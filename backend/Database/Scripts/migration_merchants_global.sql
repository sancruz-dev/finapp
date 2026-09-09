-- ─────────────────────────────────────────────────────────────────────────────
-- Torna a tabela `merchants` global (compartilhada entre todos os usuários),
-- removendo `user_id` e a categoria padrão fixa (`category_id`).
-- Mescla merchants duplicados (mesmo nome, usuários diferentes) num único
-- registro canônico e reaponta transactions/merchant_aliases/merchant_review_queue.
-- ─────────────────────────────────────────────────────────────────────────────

DROP TEMPORARY TABLE IF EXISTS merchant_canon;
CREATE TEMPORARY TABLE merchant_canon AS
SELECT name, MIN(id) AS canon_id
FROM merchants
GROUP BY name;

UPDATE transactions t
JOIN merchants m ON m.id = t.merchant_id
JOIN merchant_canon mc ON mc.name = m.name
SET t.merchant_id = mc.canon_id
WHERE t.merchant_id <> mc.canon_id;

UPDATE merchant_aliases a
JOIN merchants m ON m.id = a.merchant_id
JOIN merchant_canon mc ON mc.name = m.name
SET a.merchant_id = mc.canon_id
WHERE a.merchant_id <> mc.canon_id;

UPDATE merchant_review_queue q
JOIN merchants m ON m.id = q.suggested_merchant_id
JOIN merchant_canon mc ON mc.name = m.name
SET q.suggested_merchant_id = mc.canon_id
WHERE q.suggested_merchant_id <> mc.canon_id;

DELETE m FROM merchants m
JOIN merchant_canon mc ON mc.name = m.name
WHERE m.id <> mc.canon_id;

ALTER TABLE merchants DROP FOREIGN KEY merchants_ibfk_1;
ALTER TABLE merchants DROP FOREIGN KEY merchants_ibfk_2;

ALTER TABLE merchants
    DROP KEY uq_merchant_user_name,
    DROP COLUMN user_id,
    DROP COLUMN category_id,
    ADD UNIQUE KEY uq_merchant_name (name);

DROP TEMPORARY TABLE IF EXISTS merchant_canon;
