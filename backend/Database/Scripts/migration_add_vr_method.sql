-- Adiciona o método de pagamento "VR" (Vale Alimentação/Refeição), usado tanto para
-- receitas (crédito do benefício) quanto despesas (compras pagas com o vale).
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  MODIFY COLUMN `method` ENUM('credito','debito','pix','vr') COLLATE utf8mb4_unicode_ci DEFAULT NULL;
