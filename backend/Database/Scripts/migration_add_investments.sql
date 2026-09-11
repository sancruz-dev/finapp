-- Cria as tabelas de investimentos (renda fixa) e o cache de taxas de indexadores.
-- Execute em bancos já existentes (o schema.sql já reflete essas tabelas para bancos novos).

CREATE TABLE `investments` (
  `id` int NOT NULL AUTO_INCREMENT,
  `user_id` int NOT NULL,
  `institution` varchar(120) NOT NULL,
  `asset_type` varchar(20) NOT NULL,        -- CDB | LCI | LCA | TESOURO | POUPANCA
  `indexer` varchar(20) NOT NULL,           -- CDI | SELIC | PREFIXADO | POUPANCA
  `indexer_rate` decimal(6,2) DEFAULT NULL, -- 110 = 110% CDI; taxa a.a. p/ Prefixado; NULL p/ Poupança
  `principal_amount` decimal(12,2) NOT NULL,
  `applied_at` date NOT NULL,
  `maturity_at` date DEFAULT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `investments_user_id` (`user_id`),
  CONSTRAINT `investments_ibfk_1` FOREIGN KEY (`user_id`) REFERENCES `users` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

CREATE TABLE `indexer_daily_rates` (
  `indexer` varchar(20) NOT NULL,   -- CDI | SELIC | POUPANCA
  `rate_date` date NOT NULL,
  `rate` decimal(10,6) NOT NULL,    -- taxa do período em %, como publicada pelo Bacen (série SGS)
  PRIMARY KEY (`indexer`, `rate_date`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
