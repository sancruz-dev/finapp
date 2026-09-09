-- Adiciona a coluna "installment" para guardar o texto da parcela (ex.: "Parcela 10/12"),
-- detectado na importação a partir da coluna "Parcelamento" do extrato ou do texto da descrição.
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  ADD COLUMN `installment` varchar(20) COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER `method`;
