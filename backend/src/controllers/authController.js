const bcrypt = require('bcrypt');
const jwt = require('jsonwebtoken');
const db = require('../db');

exports.login = async (req, res) => {
  const { email, password } = req.body;
  try {
    const [rows] = await db.query('SELECT * FROM users WHERE email = ?', [email]);
    const user = rows[0];
    if (!user || !(await bcrypt.compare(password, user.password_hash))) {
      return res.status(401).json({ error: 'Email ou senha inválidos' });
    }
    const token = jwt.sign(
      { id: user.id, name: user.name, email: user.email },
      process.env.JWT_SECRET,
      { expiresIn: process.env.JWT_EXPIRES_IN }
    );
    res.json({ token, user: { id: user.id, name: user.name, email: user.email } });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.register = async (req, res) => {
  const { name, email, password } = req.body;
  try {
    const password_hash = await bcrypt.hash(password, 10);
    const [result] = await db.query(
      'INSERT INTO users (name, email, password_hash) VALUES (?, ?, ?)',
      [name, email, password_hash]
    );
    const userId = result.insertId;
    // Inserir categorias padrão para o novo usuário
    const defaultCategories = [
      ['Salário', 'income', '#22c55e'], ['Freelance', 'income', '#3b82f6'],
      ['Alimentação', 'expense', '#f97316'], ['Transporte', 'expense', '#8b5cf6'],
      ['Moradia', 'expense', '#ef4444'], ['Lazer', 'expense', '#ec4899'],
      ['Saúde', 'expense', '#14b8a6'],
    ];
    for (const [name, type, color] of defaultCategories) {
      await db.query(
        'INSERT INTO categories (user_id, name, type, color) VALUES (?, ?, ?, ?)',
        [userId, name, type, color]
      );
    }
    res.status(201).json({ message: 'Usuário criado com sucesso' });
  } catch (err) {
    if (err.code === 'ER_DUP_ENTRY') return res.status(409).json({ error: 'Email já cadastrado' });
    res.status(500).json({ error: err.message });
  }
};
