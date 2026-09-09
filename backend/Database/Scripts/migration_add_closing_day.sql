-- Adiciona o dia de fechamento de fatura por usuário (base do filtro de período do dashboard).
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `users`
  ADD COLUMN `closing_day` TINYINT NOT NULL DEFAULT 1 AFTER `password_hash`;

-- Ajuste o dia de fechamento de cada pessoa conforme a fatura real do cartão dela.
-- Exemplo: fatura da Sara fecha dia 27.
-- UPDATE `users` SET `closing_day` = 27 WHERE `email` = 'sarasousa@gmail.com';
