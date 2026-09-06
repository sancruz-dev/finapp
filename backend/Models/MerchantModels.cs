namespace FinApp.Api.Models;

// ── Entidades do banco ──────────────────────────────────────────────────────

public class Merchant
{
    public int     Id         { get; set; }
    public int     UserId     { get; set; }
    public string  Name       { get; set; } = "";
    public int?    CategoryId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}

public class MerchantAlias
{
    public int     Id         { get; set; }
    public int     MerchantId { get; set; }
    public string  RawName    { get; set; } = "";
    public string  CleanName  { get; set; } = "";
    public string  Source     { get; set; } = "manual"; // manual | ml | import
    public float?  Confidence { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class MerchantReviewItem
{
    public int     Id                   { get; set; }
    public int     UserId               { get; set; }
    public int     TransactionId        { get; set; }
    public string  RawName              { get; set; } = "";
    public string  CleanName            { get; set; } = "";
    public int?    SuggestedMerchantId  { get; set; }
    public string? SuggestedName        { get; set; }
    public float?  Confidence           { get; set; }
    public string  Status               { get; set; } = "pending";
    public DateTime CreatedAt           { get; set; }
}

// ── DTOs de request/response ────────────────────────────────────────────────

/// Resultado da predição do ML para um nome bruto
public class MerchantPrediction
{
    public string  CleanName   { get; set; } = "";   // nome após limpeza
    public int?    MerchantId  { get; set; }          // null se não encontrado
    public string? MerchantName { get; set; }         // nome canônico sugerido
    public float   Confidence  { get; set; }          // 0.0 a 1.0
    public bool    AutoResolved => Confidence >= 0.85f && MerchantId.HasValue;
}

/// Criação de um comerciante canônico
public class CreateMerchantRequest
{
    public string  Name       { get; set; } = "";
    public int?    CategoryId { get; set; }
}

/// Atualização da categoria padrão de um comerciante existente
public class UpdateMerchantCategoryRequest
{
    public int? CategoryId { get; set; }
}

/// Resolução manual de um item da fila de revisão
public class ResolveReviewRequest
{
    public int    ReviewId   { get; set; }
    public int?   MerchantId { get; set; }   // ID de um merchant existente, OU
    public string? NewName   { get; set; }   // criar novo merchant com esse nome
    public int?    CategoryId { get; set; }  // categoria do novo merchant
}

/// Item da fila de revisão para exibição na tela
public class ReviewQueueItem
{
    public int     Id                  { get; set; }
    public int     TransactionId       { get; set; }
    public string  RawName             { get; set; } = "";
    public string  CleanName           { get; set; } = "";
    public string? SuggestedName       { get; set; }
    public float?  Confidence          { get; set; }
    public decimal TransactionAmount   { get; set; }
    public string  TransactionDate     { get; set; } = "";
    public DateTime CreatedAt          { get; set; }
}

// ── ML.NET Input/Output ─────────────────────────────────────────────────────

/// Entrada para o modelo ML.NET
public class MerchantTrainingData
{
    public string CleanName  { get; set; } = "";   // feature: nome limpo
    public string Label      { get; set; } = "";   // target: nome canônico do merchant
}

/// Saída do modelo ML.NET
public class MerchantPredictionOutput
{
    public string PredictedLabel { get; set; } = "";
    public float[] Score         { get; set; } = [];
}
