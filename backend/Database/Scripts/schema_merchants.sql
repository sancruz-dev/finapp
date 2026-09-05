-- ─────────────────────────────────────────────────────────────────────────────
-- FinApp — Normalização de Comerciantes (ML)
-- Rodar após o schema existente
-- ─────────────────────────────────────────────────────────────────────────────

-- Comerciantes canônicos (o nome "oficial" que você define)
CREATE TABLE IF NOT EXISTS merchants (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    user_id     INT          NOT NULL,
    name        VARCHAR(100) NOT NULL,          -- "Oliveira Mini", "Shopee", "Dia Supermercado"
    category_id INT          NULL,              -- categoria padrão desse comerciante (FK existente)
    created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    UNIQUE KEY uq_merchant_user_name (user_id, name),
    FOREIGN KEY (user_id)     REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL
);

-- Aliases: nomes brutos que já foram resolvidos para um comerciante canônico
-- Esses registros são os DADOS DE TREINO do modelo ML
CREATE TABLE IF NOT EXISTS merchant_aliases (
    id          INT AUTO_INCREMENT PRIMARY KEY,
    merchant_id INT          NOT NULL,
    raw_name    VARCHAR(255) NOT NULL,          -- "ALEMAO MINI MERCADOSAO PAULOBRA"
    clean_name  VARCHAR(255) NOT NULL,          -- "ALEMAO MINI MERCADO" (após limpeza)
    source      ENUM('manual','ml','import') NOT NULL DEFAULT 'manual',
    confidence  DECIMAL(5,4) NULL,             -- confiança do ML (NULL se manual)
    created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    UNIQUE KEY uq_alias_raw (raw_name),
    FOREIGN KEY (merchant_id) REFERENCES merchants(id) ON DELETE CASCADE
);

-- Fila de revisão: lançamentos que o ML não conseguiu resolver com confiança suficiente
CREATE TABLE IF NOT EXISTS merchant_review_queue (
    id             INT AUTO_INCREMENT PRIMARY KEY,
    user_id        INT          NOT NULL,
    transaction_id INT          NOT NULL,
    raw_name       VARCHAR(255) NOT NULL,
    clean_name     VARCHAR(255) NOT NULL,
    suggested_merchant_id INT   NULL,          -- sugestão do ML (pode ser NULL)
    suggested_name        VARCHAR(100) NULL,   -- nome canônico sugerido
    confidence     DECIMAL(5,4) NULL,
    status         ENUM('pending','approved','rejected') NOT NULL DEFAULT 'pending',
    reviewed_at    TIMESTAMP NULL,
    created_at     TIMESTAMP DEFAULT CURRENT_TIMESTAMP,

    -- Único por transação: permite ON DUPLICATE KEY UPDATE ao reprocessar a mesma transação
    UNIQUE KEY uq_review_transaction (transaction_id),
    FOREIGN KEY (user_id)        REFERENCES users(id) ON DELETE CASCADE,
    FOREIGN KEY (transaction_id) REFERENCES transactions(id) ON DELETE CASCADE,
    FOREIGN KEY (suggested_merchant_id) REFERENCES merchants(id) ON DELETE SET NULL
);

-- Adiciona merchant_id na tabela transactions existente
ALTER TABLE transactions
    ADD COLUMN merchant_id INT NULL AFTER category_id,
    ADD CONSTRAINT fk_transaction_merchant
        FOREIGN KEY (merchant_id) REFERENCES merchants(id) ON DELETE SET NULL;

-- Índice para acelerar queries por comerciante
CREATE INDEX idx_transactions_merchant ON transactions(merchant_id);