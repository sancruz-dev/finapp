-- Adiciona um identificador de grupo às transações parceladas, para permitir
-- apagar todas as parcelas de uma compra de uma vez (em vez de só uma unidade).
-- Séries criadas antes desta migration ficam com installment_group_id NULL
-- (não é possível inferir com segurança quais linhas antigas pertencem à mesma série).

SET @dbname = DATABASE();
SET @tablename = 'transactions';
SET @columnname = 'installment_group_id';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = @tablename
      AND COLUMN_NAME = @columnname
  ) > 0,
  'SELECT 1',
  'ALTER TABLE `transactions` ADD COLUMN `installment_group_id` char(36) DEFAULT NULL AFTER `installment`'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;

SET @indexname = 'idx_transactions_installment_group_id';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.STATISTICS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = @tablename
      AND INDEX_NAME = @indexname
  ) > 0,
  'SELECT 1',
  'CREATE INDEX `idx_transactions_installment_group_id` ON `transactions` (`installment_group_id`)'
));
PREPARE createIndexIfNotExists FROM @preparedStatement;
EXECUTE createIndexIfNotExists;
DEALLOCATE PREPARE createIndexIfNotExists;
