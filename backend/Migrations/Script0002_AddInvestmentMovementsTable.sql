-- Cria a tabela de aportes/resgates de um investimento já existente.
-- Depende de `investments` (Script0001) já existir. IF NOT EXISTS: seguro
-- rodar em bancos que já têm essa tabela.

CREATE TABLE IF NOT EXISTS `investment_movements` (
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
