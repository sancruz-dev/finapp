-- Adiciona um motivo opcional a cada aporte/resgate (ex.: "emergência", "13º salário").
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).

ALTER TABLE `investment_movements`
  ADD COLUMN `reason` varchar(255) DEFAULT NULL AFTER `movement_date`;
