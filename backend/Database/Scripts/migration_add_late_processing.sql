-- Marca uma transação como "processamento tardio": uma compra feita perto do fechamento
-- da fatura que a operadora processou/registrou apenas no mês seguinte. Quando marcada,
-- o dashboard mostra a transação no período do mês seguinte (em vez do período em que a
-- data efetivamente cai) e exibe uma tag indicando o motivo.
-- Execute em bancos já existentes (o schema.sql já reflete essa coluna para bancos novos).
ALTER TABLE `transactions`
  ADD COLUMN `late_processing` tinyint(1) NOT NULL DEFAULT 0 AFTER `installment`;
