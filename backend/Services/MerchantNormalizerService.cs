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

    // O catálogo de merchants/aliases é global (compartilhado por todos os
    // usuários), então o modelo ML também é único e compartilhado.
    private PredictionEngine<MerchantTrainingData, MerchantPredictionOutput>? _predEngine;
    private readonly SemaphoreSlim _trainLock = new(1, 1);

    // Modelo treinado é persistido em disco para sobreviver a restart da API
    // (sem isso, o serviço voltaria a treinar do zero — e sem sugestões — a cada deploy/reinício).
    private static readonly string ModelDirectory = Path.Combine(AppContext.BaseDirectory, "MlModels");
    private static readonly string ModelPath = Path.Combine(ModelDirectory, "merchant-model.zip");

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

        // 1. Tenta match exato no banco (mais confiável, sem ML) — catálogo global
        var exactMatch = await FindExactAliasAsync(rawName, cleanName);
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

        // 2. Se não tem modelo treinado ainda (nem em memória, nem em disco), retorna sem sugestão
        var predEngine = _predEngine ?? await TryLoadOrTrainModelAsync();
        if (predEngine is null)
        {
            return new MerchantPrediction { CleanName = cleanName, Confidence = 0f };
        }

        // 3. Predição via ML.NET
        var input  = new MerchantTrainingData { CleanName = cleanName };
        var output = predEngine.Predict(input);

        var confidence = output.Score.Length > 0 ? output.Score.Max() : 0f;

        // Busca o merchant pelo nome predito
        var merchant = await FindMerchantByNameAsync(output.PredictedLabel);

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
                INSERT INTO merchants (name)
                VALUES (@Name)
                ON DUPLICATE KEY UPDATE id = LAST_INSERT_ID(id);
                SELECT LAST_INSERT_ID();",
                new { Name = request.NewName });
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
        _ = Task.Run(() => TrainModelAsync());
    }

    // ── CRUD de merchants (catálogo global) ─────────────────────────────────

    public async Task<IEnumerable<Merchant>> ListMerchantsAsync()
    {
        using var conn = db.Create();
        return await conn.QueryAsync<Merchant>("SELECT * FROM merchants ORDER BY name");
    }

    public async Task<Merchant> CreateMerchantAsync(CreateMerchantRequest req)
    {
        using var conn = db.Create();
        var id = await conn.ExecuteScalarAsync<int>(@"
            INSERT INTO merchants (name)
            VALUES (@Name);
            SELECT LAST_INSERT_ID();",
            new { Name = req.Name });

        return new Merchant { Id = id, Name = req.Name };
    }

    public async Task AddAliasAsync(int merchantId, string rawName)
    {
        var cleanName = CleanRawName(rawName);
        using var conn = db.Create();

        var exists = await conn.ExecuteScalarAsync<int>(
            "SELECT COUNT(*) FROM merchants WHERE id = @Id",
            new { Id = merchantId });
        if (exists == 0) throw new KeyNotFoundException("Comerciante não encontrado.");

        await conn.ExecuteAsync(@"
            INSERT IGNORE INTO merchant_aliases (merchant_id, raw_name, clean_name, source)
            VALUES (@MerchantId, @RawName, @CleanName, 'manual')",
            new { MerchantId = merchantId, RawName = rawName, CleanName = cleanName });

        _ = Task.Run(() => TrainModelAsync());
    }

    // ── Backfill do histórico ───────────────────────────────────────────────

    /// <summary>
    /// Processa (via ML/fila de revisão) todos os lançamentos existentes do
    /// usuário que ainda não têm merchant_id resolvido. Útil para popular a
    /// fila de revisão pela primeira vez, com o histórico já importado.
    /// </summary>
    public async Task<int> BackfillAsync(int userId)
    {
        using var conn = db.Create();
        var pending = (await conn.QueryAsync<(int Id, string Description)>(@"
            SELECT id AS Id, description AS Description
            FROM transactions
            WHERE user_id = @UserId
              AND merchant_id IS NULL
              AND description IS NOT NULL
              AND description <> ''",
            new { UserId = userId })).ToList();

        foreach (var tx in pending)
            await ProcessTransactionAsync(tx.Id, tx.Description, userId);

        return pending.Count;
    }

    // ── Fila de revisão ────────────────────────────────────────────────────

    public async Task<IEnumerable<ReviewQueueItem>> GetReviewQueueAsync(int userId)
    {
        using var conn = db.Create();
        var items = (await conn.QueryAsync<ReviewQueueItem>(@"
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
            new { UserId = userId })).ToList();

        // A sugestão gravada é uma foto do momento em que o item entrou na fila.
        // O modelo pode ter mudado desde então (novos comerciantes, retreinos,
        // exclusões) — recalcula ao vivo antes de devolver, e atualiza o registro
        // salvo para não ficar desatualizado de novo na próxima leitura.
        foreach (var item in items)
        {
            var prediction = await PredictAsync(item.RawName, userId);
            item.SuggestedName = prediction.MerchantName;
            item.Confidence    = prediction.Confidence;

            await conn.ExecuteAsync(@"
                UPDATE merchant_review_queue
                SET suggested_merchant_id = @MerchantId,
                    suggested_name        = @MerchantName,
                    confidence            = @Confidence
                WHERE id = @Id",
                new {
                    MerchantId   = prediction.MerchantId,
                    MerchantName = prediction.MerchantName,
                    Confidence   = prediction.Confidence,
                    Id           = item.Id,
                });
        }

        return items;
    }

    // ── Treino do modelo ML.NET ────────────────────────────────────────────

    /// <summary>
    /// Treina o modelo com todos os aliases confirmados do usuário.
    /// Chamado automaticamente após cada resolução manual.
    /// </summary>
    public async Task TrainModelAsync()
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
                JOIN merchants m ON m.id = a.merchant_id")).ToList();

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

            var model      = pipeline.Fit(dataView);
            var predEngine = _mlContext.Model.CreatePredictionEngine<MerchantTrainingData, MerchantPredictionOutput>(model);
            _predEngine = predEngine;

            // Persiste em disco para não precisar retreinar do zero a cada restart da API
            try
            {
                Directory.CreateDirectory(ModelDirectory);
                _mlContext.Model.Save(model, dataView.Schema, ModelPath);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Modelo treinado, mas falhou ao salvar em disco.");
            }

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

    /// <summary>
    /// Tenta obter uma prediction engine pronta para o usuário: primeiro carregando
    /// um modelo já treinado do disco (restart da API), senão treinando do zero
    /// a partir dos aliases confirmados no banco.
    /// </summary>
    private async Task<PredictionEngine<MerchantTrainingData, MerchantPredictionOutput>?> TryLoadOrTrainModelAsync()
    {
        if (File.Exists(ModelPath))
        {
            try
            {
                var model  = _mlContext.Model.Load(ModelPath, out _);
                var engine = _mlContext.Model.CreatePredictionEngine<MerchantTrainingData, MerchantPredictionOutput>(model);
                _predEngine = engine;
                return engine;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao carregar modelo ML do disco; retreinando.");
            }
        }

        try { await TrainModelAsync(); }
        catch (Exception ex) { logger.LogWarning(ex, "Não foi possível treinar modelo."); }

        return _predEngine;
    }

    private async Task<Merchant?> FindExactAliasAsync(string rawName, string cleanName)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<Merchant>(@"
            SELECT m.* FROM merchants m
            JOIN merchant_aliases a ON a.merchant_id = m.id
            WHERE a.raw_name = @RawName OR a.clean_name = @CleanName
            LIMIT 1",
            new { RawName = rawName, CleanName = cleanName });
    }

    private async Task<Merchant?> FindMerchantByNameAsync(string name)
    {
        using var conn = db.Create();
        return await conn.QueryFirstOrDefaultAsync<Merchant>(
            "SELECT * FROM merchants WHERE name = @Name LIMIT 1",
            new { Name = name });
    }
}
