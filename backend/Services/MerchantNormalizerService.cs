using Dapper;
using FinApp.Api.Data;
using FinApp.Api.Models;
using Microsoft.ML;
using Microsoft.ML.Data;
using System.Text.RegularExpressions;

namespace FinApp.Api.Services;

/// <summary>
/// Serviço responsável por:
/// 1. Limpar nomes brutos de lançamentos (regex)
/// 2. Predizer o comerciante canônico usando ML.NET
/// 3. Treinar/retreinar o modelo com os aliases confirmados
/// 4. Gerenciar a fila de revisão manual
/// </summary>
public class MerchantNormalizerService(DbConnectionFactory db, ILogger<MerchantNormalizerService> logger)
{
    private readonly MLContext _mlContext = new(seed: 42);

    // O modelo fica em memória após o primeiro treino
    private ITransformer?       _model;
    private PredictionEngine<MerchantTrainingData, MerchantPredictionOutput>? _predEngine;
    private readonly SemaphoreSlim _trainLock = new(1, 1);

    // Limiar de confiança para resolução automática
    private const float AutoResolveThreshold = 0.85f;

    // ── Limpeza de nome ────────────────────────────────────────────────────

    /// <summary>
    /// Remove sufixos de cidade/estado colados pelo processador do cartão.
    /// Ex: "ALEMAO MINI MERCADOSAO PAULOBRA" → "ALEMAO MINI MERCADO"
    /// </summary>
    public static string CleanRawName(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return raw;

        var s = raw.ToUpperInvariant().Trim();

        // Remove sufixos geográficos brasileiros comuns nos extratos
        // Ex: "SAO PAULOBRA", "RIO DE JANEIROBRA", "CURITIBABRA", "BRA"
        s = Regex.Replace(s,
            @"(SAO PAULO|RIO DE JANEIRO|BELO HORIZONTE|CURITIBA|BRASILIA|" +
            @"FORTALEZA|MANAUS|SALVADOR|RECIFE|PORTO ALEGRE|CAMPINAS|" +
            @"GOIANIA|BELÉM|MACEIO|NATAL|TERESINA|CAMPO GRANDE|" +
            @"JOAO PESSOA|ARACAJU|PORTO VELHO|MACAPA|BOA VISTA|PALMAS|" +
            @"FLORIANOPOLIS|VITORIA|CUIABA)[A-Z]{0,5}$",
            "", RegexOptions.IgnoreCase);

        // Remove código de país no final: "BRA", "BR"
        s = Regex.Replace(s, @"\s*(BRA|BR)\s*$", "");

        // Remove caracteres especiais desnecessários (mantém letras, números, espaço, * /)
        s = Regex.Replace(s, @"[^\w\s\*\/\-]", " ");

        // Colapsa múltiplos espaços
        s = Regex.Replace(s, @"\s{2,}", " ").Trim();

        return s;
    }

    // ── Predição ───────────────────────────────────────────────────────────

    /// <summary>
    /// Dado o nome bruto de um lançamento, retorna o comerciante canônico predito.
    /// Se não houver modelo treinado ainda, tenta match exato nos aliases.
    /// </summary>
    public async Task<MerchantPrediction> PredictAsync(string rawName, int userId)
    {
        var cleanName = CleanRawName(rawName);

        // 1. Tenta match exato no banco (mais confiável, sem ML)
        var exactMatch = await FindExactAliasAsync(rawName, cleanName, userId);
        if (exactMatch is not null)
        {
            return new MerchantPrediction
            {
                CleanName    = cleanName,
                MerchantId   = exactMatch.Id,
                MerchantName = exactMatch.Name,
                Confidence   = 1.0f,
            };
        }

        // 2. Se não tem modelo treinado ainda, retorna sem sugestão
        if (_predEngine is null)
        {
            await TryLoadOrTrainModelAsync(userId);
            if (_predEngine is null)
            {
                return new MerchantPrediction { CleanName = cleanName, Confidence = 0f };
            }
        }

        // 3. Predição via ML.NET
        var input  = new MerchantTrainingData { CleanName = cleanName };
        var output = _predEngine.Predict(input);

        var confidence = output.Score.Length > 0 ? output.Score.Max() : 0f;

        // Busca o merchant pelo nome predito
        var merchant = await FindMerchantByNameAsync(output.PredictedLabel, userId);

        return new MerchantPrediction
        {
            CleanName    = cleanName,
            MerchantId   = merchant?.Id,
            MerchantName = merchant?.Name ?? output.PredictedLabel,
            Confidence   = confidence,
        };
    }

    // ── Processamento de lançamento ────────────────────────────────────────

    /// <summary>
    /// Processa um lançamento recém-criado:
    /// - Se confiança alta → atribui merchant_id automaticamente
    /// - Se confiança baixa → envia para fila de revisão
    /// </summary>
    public async Task ProcessTransactionAsync(int transactionId, string rawDescription, int userId)
    {
        var prediction = await PredictAsync(rawDescription, userId);

        using var conn = db.Create();

        if (prediction.AutoResolved)
        {
            // Atribuição automática
            await conn.ExecuteAsync(@"
                UPDATE transactions SET merchant_id = @MerchantId WHERE id = @Id",
                new { MerchantId = prediction.MerchantId, Id = transactionId });

            logger.LogInformation(
                "Transação {TxId} → merchant '{Name}' (confiança {C:P0})",
                transactionId, prediction.MerchantName, prediction.Confidence);
        }
        else
        {
            // Envia para revisão manual
            await conn.ExecuteAsync(@"
                INSERT INTO merchant_review_queue
                    (user_id, transaction_id, raw_name, clean_name,
                     suggested_merchant_id, suggested_name, confidence)
                VALUES
                    (@UserId, @TxId, @RawName, @CleanName,
                     @MerchantId, @MerchantName, @Confidence)
                ON DUPLICATE KEY UPDATE
                    suggested_merchant_id = @MerchantId,
                    suggested_name        = @MerchantName,
                    confidence            = @Confidence",
                new {
                    UserId       = userId,
                    TxId         = transactionId,
                    RawName      = rawDescription,
                    CleanName    = prediction.CleanName,
                    MerchantId   = prediction.MerchantId,
                    MerchantName = prediction.MerchantName,
                    Confidence   = prediction.Confidence,
                });

            logger.LogInformation(
                "Transação {TxId} → fila de revisão (confiança {C:P0}, sugestão: '{Name}')",
                transactionId, prediction.Confidence, prediction.MerchantName);
        }
    }

    // ── Resolução manual (fila de revisão) ────────────────────────────────

    /// <summary>
    /// Usuário confirma/corrige um item da fila de revisão.
    /// Isso cria o alias no banco e agenda retreino do modelo.
    /// </summary>
    public async Task ResolveReviewAsync(ResolveReviewRequest request, int userId)
    {
        using var conn = db.Create();

        // Busca item da fila
        var item = await conn.QueryFirstOrDefaultAsync<MerchantReviewItem>(@"
            SELECT * FROM merchant_review_queue
            WHERE id = @Id AND user_id = @UserId AND status = 'pending'",
            new { Id = request.ReviewId, UserId = userId });

        if (item is null) throw new KeyNotFoundException("Item de revisão não encontrado.");

        // Resolve o merchant: usa existente ou cria novo
        int merchantId;
        if (request.MerchantId.HasValue)
        {
            merchantId = request.MerchantId.Value;
        }
        else if (!string.IsNullOrWhiteSpace(request.NewName))
        {
            merchantId = await conn.ExecuteScalarAsync<int>(@"
                INSERT INTO merchants (user_id, name, category_id)
                VALUES (@UserId, @Name, @CategoryId);
                SELECT LAST_INSERT_ID();",
                new { UserId = userId, Name = request.NewName, CategoryId = request.CategoryId });
        }
        else
        {
            throw new ArgumentException("Informe MerchantId ou NewName.");
        }

        // Salva o alias (dado de treino)
        await conn.ExecuteAsync(@"
            INSERT IGNORE INTO merchant_aliases
                (merchant_id, raw_name, clean_name, source, confidence)
            VALUES
                (@MerchantId, @RawName, @CleanName, 'manual', 1.0)",
            new { MerchantId = merchantId, RawName = item.RawName, CleanName = item.CleanName });

        // Atualiza a transação
        await conn.ExecuteAsync(@"
            UPDATE transactions SET merchant_id = @MerchantId WHERE id = @TxId",
            new { MerchantId = merchantId, TxId = item.TransactionId });

        // Marca como aprovado
        await conn.ExecuteAsync(@"
            UPDATE merchant_review_queue
            SET status = 'approved', reviewed_at = NOW()
            WHERE id = @Id",
            new { Id = request.ReviewId });

        // Agenda retreino em background (não bloqueia a resposta)
        _ = Task.Run(() => TrainModelAsync(userId));
    }

    // ── CRUD de merchants ──────────────────────────────────────────────────

    public async Task<IEnumerable<Merchant>> ListMerchantsAsync(int userId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Merchant>(
            "SELECT * FROM merchants WHERE user_id = @UserId ORDER BY name",
            new { UserId = userId });
    }

    public async Task<Merchant> CreateMerchantAsync(CreateMerchantRequest req, int userId)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO merchants (user_id, name, category_id)
            VALUES (@UserId, @Name, @CategoryId);
            SELECT LAST_INSERT_ID();",
            new { UserId = userId, Name = req.Name, CategoryId = req.CategoryId });

        return new Merchant { Id = id, UserId = userId, Name = req.Name, CategoryId = req.CategoryId };
    }

    public async Task AddAliasAsync(int merchantId, string rawName, int userId)
    {
        var cleanName = CleanRawName(rawName);
        using var conn = db.Create();

        // Verifica que o merchant pertence ao usuário
        var owns = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM merchants WHERE id = @Id AND user_id = @UserId",
            new { Id = merchantId, UserId = userId });
        if (owns == 0) throw new UnauthorizedAccessException();

        await conn.ExecuteAsync(@"
            INSERT IGNORE INTO merchant_aliases (merchant_id, raw_name, clean_name, source)
            VALUES (@MerchantId, @RawName, @CleanName, 'manual')",
            new { MerchantId = merchantId, RawName = rawName, CleanName = cleanName });

        _ = Task.Run(() => TrainModelAsync(userId));
    }

    // ── Fila de revisão ────────────────────────────────────────────────────

    public async Task<IEnumerable<ReviewQueueItem>> GetReviewQueueAsync(int userId)
    {
        using var conn = db.Create();
        return await conn.QueryAsync<ReviewQueueItem>(@"
            SELECT
                q.id,
                q.transaction_id,
                q.raw_name,
                q.clean_name,
                q.suggested_name,
                q.confidence,
                t.amount  AS transaction_amount,
                t.date    AS transaction_date,
                q.created_at
            FROM merchant_review_queue q
            JOIN transactions t ON t.id = q.transaction_id
            WHERE q.user_id = @UserId AND q.status = 'pending'
            ORDER BY q.created_at DESC",
            new { UserId = userId });
    }

    // ── Treino do modelo ML.NET ────────────────────────────────────────────

    /// <summary>
    /// Treina o modelo com todos os aliases confirmados do usuário.
    /// Chamado automaticamente após cada resolução manual.
    /// </summary>
    public async Task TrainModelAsync(int userId)
    {
        await _trainLock.WaitAsync();
        try
        {
            using var conn = db.Create();
            var trainingData = (await conn.QueryAsync<MerchantTrainingData>(@"
                SELECT
                    a.clean_name AS CleanName,
                    m.name       AS Label
                FROM merchant_aliases a
                JOIN merchants m ON m.id = a.merchant_id
                WHERE m.user_id = @UserId",
                new { UserId = userId })).ToList();

            if (trainingData.Count < 5)
            {
                logger.LogInformation(
                    "ML: apenas {N} aliases — treino adiado (mínimo 5).", trainingData.Count);
                return;
            }

            var dataView = _mlContext.Data.LoadFromEnumerable(trainingData);
            var featurizeOptions = new Microsoft.ML.Transforms.Text.TextFeaturizingEstimator.Options
            {
                CharFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
                {
                    NgramLength   = 3,
                    UseAllLengths = true,
                },
                WordFeatureExtractor = new Microsoft.ML.Transforms.Text.WordBagEstimator.Options
                {
                    NgramLength   = 2,
                    UseAllLengths = true,
                },
            };

            var pipeline = _mlContext.Transforms.Text
                .FeaturizeText("Features", featurizeOptions, nameof(MerchantTrainingData.CleanName))
                .Append(_mlContext.Transforms.Conversion.MapValueToKey("Label"))
                .Append(_mlContext.MulticlassClassification.Trainers.SdcaMaximumEntropy(
                    labelColumnName: "Label",
                    featureColumnName: "Features"))
                .Append(_mlContext.Transforms.Conversion.MapKeyToValue("PredictedLabel"));

            _model     = pipeline.Fit(dataView);
            _predEngine = _mlContext.Model.CreatePredictionEngine<MerchantTrainingData, MerchantPredictionOutput>(_model);

            logger.LogInformation("ML: modelo treinado com {N} aliases.", trainingData.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Erro ao treinar modelo ML.");
        }
        finally
        {
            _trainLock.Release();
        }
    }

    // ── Helpers privados ───────────────────────────────────────────────────

    private async Task TryLoadOrTrainModelAsync(int userId)
    {
        try { await TrainModelAsync(userId); }
        catch (Exception ex) { logger.LogWarning(ex, "Não foi possível treinar modelo."); }
    }

    private async Task<Merchant?> FindExactAliasAsync(string rawName, string cleanName, int userId)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<Merchant>(@"
            SELECT m.* FROM merchants m
            JOIN merchant_aliases a ON a.merchant_id = m.id
            WHERE m.user_id = @UserId
              AND (a.raw_name = @RawName OR a.clean_name = @CleanName)
            LIMIT 1",
            new { UserId = userId, RawName = rawName, CleanName = cleanName });
    }

    private async Task<Merchant?> FindMerchantByNameAsync(string name, int userId)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<Merchant>(
            "SELECT * FROM merchants WHERE user_id = @UserId AND name = @Name LIMIT 1",
            new { UserId = userId, Name = name });
    }
}
