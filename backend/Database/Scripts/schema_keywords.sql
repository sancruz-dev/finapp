-- Execute este script para adicionar suporte a palavras-chave de categorias
USE finapp;

CREATE TABLE IF NOT EXISTS category_keywords (
  id          INT AUTO_INCREMENT PRIMARY KEY,
  category_id INT NOT NULL,
  keyword     VARCHAR(100) NOT NULL,
  created_at  TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE CASCADE,
  UNIQUE KEY uq_cat_keyword (category_id, keyword)
);
