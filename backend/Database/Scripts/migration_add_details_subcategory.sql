-- Adiciona "details" (texto livre para detalhar compras variadas, ex.: itens de uma compra
-- de drogaria) e "subcategory_id" (segunda categoria opcional, ex.: categoria principal
-- "Saúde" + subcategoria "Alimentação" para o mesmo lançamento).
-- Execute em bancos já existentes (o schema.sql já reflete essas colunas para bancos novos).
ALTER TABLE `transactions`
  ADD COLUMN `details` text COLLATE utf8mb4_unicode_ci DEFAULT NULL AFTER `notes`,
  ADD COLUMN `subcategory_id` int DEFAULT NULL AFTER `category_id`,
  ADD KEY `subcategory_id` (`subcategory_id`),
  ADD CONSTRAINT `transactions_ibfk_3` FOREIGN KEY (`subcategory_id`) REFERENCES `categories` (`id`) ON DELETE SET NULL;
