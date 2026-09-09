-- Adiciona o teto mensal de gastos por categoria (usado na tela de Configurações
-- e exibido como indicador de orçamento no dashboard).
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `categories`
  ADD COLUMN `monthly_limit` DECIMAL(10,2) DEFAULT NULL AFTER `icon`;
