-- Cria a tabela de aportes/resgates de um investimento já existente.
-- Execute em bancos já existentes (o schema.sql já reflete essa tabela para bancos novos).

CREATE TABLE `investment_movements` (
  `id` int NOT NULL AUTO_INCREMENT,
  `investment_id` int NOT NULL,
  `type` varchar(10) NOT NULL,        -- APORTE | RESGATE
  `amount` decimal(12,2) NOT NULL,
  `movement_date` date NOT NULL,
  `created_at` timestamp NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (`id`),
  KEY `investment_movements_investment_id` (`investment_id`),
  CONSTRAINT `investment_movements_ibfk_1` FOREIGN KEY (`investment_id`) REFERENCES `investments` (`id`) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
