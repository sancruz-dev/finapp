const db = require('../db');

exports.list = async (req, res) => {
  const { month, year, type, category_id } = req.query;
  let query = `
    SELECT t.*, c.name as category_name, c.color as category_color, c.icon as category_icon
    FROM transactions t
    LEFT JOIN categories c ON t.category_id = c.id
    WHERE t.user_id = ?
  `;
  const params = [req.user.id];
  if (month && year) {
    query += ' AND MONTH(t.date) = ? AND YEAR(t.date) = ?';
    params.push(month, year);
  }
  if (type) { query += ' AND t.type = ?'; params.push(type); }
  if (category_id) { query += ' AND t.category_id = ?'; params.push(category_id); }
  query += ' ORDER BY t.date DESC, t.created_at DESC';

  try {
    const [rows] = await db.query(query, params);
    res.json(rows);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.create = async (req, res) => {
  const { type, amount, description, date, category_id, notes } = req.body;
  try {
    const [result] = await db.query(
      'INSERT INTO transactions (user_id, type, amount, description, date, category_id, notes) VALUES (?, ?, ?, ?, ?, ?, ?)',
      [req.user.id, type, amount, description, date, category_id || null, notes || null]
    );
    const [rows] = await db.query(
      `SELECT t.*, c.name as category_name, c.color as category_color
       FROM transactions t LEFT JOIN categories c ON t.category_id = c.id
       WHERE t.id = ?`,
      [result.insertId]
    );
    res.status(201).json(rows[0]);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.update = async (req, res) => {
  const { id } = req.params;
  const { type, amount, description, date, category_id, notes } = req.body;
  try {
    await db.query(
      'UPDATE transactions SET type=?, amount=?, description=?, date=?, category_id=?, notes=? WHERE id=? AND user_id=?',
      [type, amount, description, date, category_id || null, notes || null, id, req.user.id]
    );
    res.json({ message: 'Atualizado com sucesso' });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.remove = async (req, res) => {
  const { id } = req.params;
  try {
    await db.query('DELETE FROM transactions WHERE id = ? AND user_id = ?', [id, req.user.id]);
    res.json({ message: 'Removido com sucesso' });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.summary = async (req, res) => {
  const { month, year } = req.query;
  try {
    const [rows] = await db.query(
      `SELECT 
        SUM(CASE WHEN type = 'income' THEN amount ELSE 0 END) as total_income,
        SUM(CASE WHEN type = 'expense' THEN amount ELSE 0 END) as total_expense,
        SUM(CASE WHEN type = 'income' THEN amount ELSE -amount END) as balance
       FROM transactions
       WHERE user_id = ? AND MONTH(date) = ? AND YEAR(date) = ?`,
      [req.user.id, month, year]
    );
    // Gastos por categoria
    const [byCategory] = await db.query(
      `SELECT c.name, c.color, SUM(t.amount) as total
       FROM transactions t
       JOIN categories c ON t.category_id = c.id
       WHERE t.user_id = ? AND t.type = 'expense' AND MONTH(t.date) = ? AND YEAR(t.date) = ?
       GROUP BY c.id ORDER BY total DESC`,
      [req.user.id, month, year]
    );
    res.json({ ...rows[0], by_category: byCategory });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};
