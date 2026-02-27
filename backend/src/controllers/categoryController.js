const db = require('../db');

exports.list = async (req, res) => {
  try {
    const [rows] = await db.query(
      'SELECT * FROM categories WHERE user_id = ? ORDER BY type, name',
      [req.user.id]
    );
    res.json(rows);
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.create = async (req, res) => {
  const { name, type, color, icon } = req.body;
  try {
    const [result] = await db.query(
      'INSERT INTO categories (user_id, name, type, color, icon) VALUES (?, ?, ?, ?, ?)',
      [req.user.id, name, type, color || '#6366f1', icon || null]
    );
    res.status(201).json({ id: result.insertId, name, type, color, icon });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};

exports.remove = async (req, res) => {
  const { id } = req.params;
  try {
    await db.query('DELETE FROM categories WHERE id = ? AND user_id = ?', [id, req.user.id]);
    res.json({ message: 'Removido com sucesso' });
  } catch (err) {
    res.status(500).json({ error: err.message });
  }
};
