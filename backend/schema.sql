CREATE DATABASE IF NOT EXISTS finapp CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci;
USE finapp;

CREATE TABLE users (
  id INT AUTO_INCREMENT PRIMARY KEY,
  name VARCHAR(100) NOT NULL,
  email VARCHAR(150) NOT NULL UNIQUE,
  password_hash VARCHAR(255) NOT NULL,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP
);

CREATE TABLE categories (
  id INT AUTO_INCREMENT PRIMARY KEY,
  user_id INT,
  name VARCHAR(100) NOT NULL,
  type ENUM('income', 'expense') NOT NULL,
  color VARCHAR(7) DEFAULT '#6366f1',
  icon VARCHAR(50),
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE
);

CREATE TABLE transactions (
  id INT AUTO_INCREMENT PRIMARY KEY,
  user_id INT NOT NULL,
  category_id INT,
  type ENUM('income', 'expense') NOT NULL,
  amount DECIMAL(10, 2) NOT NULL,
  description VARCHAR(255),
  date DATE NOT NULL,
  notes TEXT,
  created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
  updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
  FOREIGN KEY (user_id) REFERENCES users(id) ON DELETE CASCADE,
  FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE SET NULL
);

-- Categorias padrão (serão inseridas para cada novo usuário via trigger ou seed)
INSERT INTO users (name, email, password_hash) VALUES
  ('Usuário 1', 'user1@finapp.local', '$2b$10$placeholder'),
  ('Usuário 2', 'user2@finapp.local', '$2b$10$placeholder');

INSERT INTO categories (user_id, name, type, color, icon) VALUES
  (1, 'Salário', 'income', '#22c55e', 'briefcase'),
  (1, 'Freelance', 'income', '#3b82f6', 'laptop'),
  (1, 'Alimentação', 'expense', '#f97316', 'utensils'),
  (1, 'Transporte', 'expense', '#8b5cf6', 'car'),
  (1, 'Moradia', 'expense', '#ef4444', 'home'),
  (1, 'Lazer', 'expense', '#ec4899', 'gamepad'),
  (1, 'Saúde', 'expense', '#14b8a6', 'heart'),
  (2, 'Salário', 'income', '#22c55e', 'briefcase'),
  (2, 'Freelance', 'income', '#3b82f6', 'laptop'),
  (2, 'Alimentação', 'expense', '#f97316', 'utensils'),
  (2, 'Transporte', 'expense', '#8b5cf6', 'car'),
  (2, 'Moradia', 'expense', '#ef4444', 'home'),
  (2, 'Lazer', 'expense', '#ec4899', 'gamepad'),
  (2, 'Saúde', 'expense', '#14b8a6', 'heart');
