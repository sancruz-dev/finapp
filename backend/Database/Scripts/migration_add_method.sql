-- Adiciona a coluna de método de pagamento à tabela transactions.
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  ADD COLUMN `method` ENUM('credito','debito','pix') COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER `date`;

-- Transações já importadas de fatura (identificadas antes desta migração) não têm como saber
-- retroativamente a origem; ajuste manualmente se necessário.
