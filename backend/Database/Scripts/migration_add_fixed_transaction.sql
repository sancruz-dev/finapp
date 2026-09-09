-- Adiciona a coluna "fixed" para marcar transações fixas (aluguel, assinaturas, etc.),
-- com valor 'S' (sim) ou 'N' (não), padrão 'N'.
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  ADD COLUMN `fixed` ENUM('S','N') COLLATE utf8mb4_unicode_ci NOT NULL DEFAULT 'N' AFTER `installment`;
