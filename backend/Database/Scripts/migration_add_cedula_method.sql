-- Adiciona o método de pagamento "Cédula" (dinheiro em espécie).
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  MODIFY COLUMN `method` ENUM('credito','debito','pix','vr','cedula') COLLATE utf8mb4_unicode_ci DEFAULT NULL;
