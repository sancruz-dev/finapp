-- Adiciona um motivo opcional a cada aporte/resgate (ex.: "emergência", "13º salário").
-- Depende de `investment_movements` (Script0002) já existir. MySQL não
-- suporta `ADD COLUMN IF NOT EXISTS` (é extensão do MariaDB) — por isso a
-- checagem via information_schema abaixo, para ser seguro rodar em bancos
-- que já têm essa coluna.

SET @dbname = DATABASE();
SET @tablename = 'investment_movements';
SET @columnname = 'reason';
SET @preparedStatement = (SELECT IF(
  (
    SELECT COUNT(*) FROM INFORMATION_SCHEMA.COLUMNS
    WHERE TABLE_SCHEMA = @dbname
      AND TABLE_NAME = @tablename
      AND COLUMN_NAME = @columnname
  ) > 0,
  'SELECT 1',
  'ALTER TABLE `investment_movements` ADD COLUMN `reason` varchar(255) DEFAULT NULL AFTER `movement_date`'
));
PREPARE alterIfNotExists FROM @preparedStatement;
EXECUTE alterIfNotExists;
DEALLOCATE PREPARE alterIfNotExists;
