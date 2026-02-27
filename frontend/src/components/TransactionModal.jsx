import React, { useState, useEffect } from 'react';
import dayjs from 'dayjs';
import { categoryService } from '../services/api';

export default function TransactionModal({ initial, onSave, onClose }) {
  const [form, setForm] = useState({
    type: 'expense',
    amount: '',
    description: '',
    date: dayjs().format('YYYY-MM-DD'),
    category_id: '',
    notes: '',
    ...initial,
    amount: initial?.amount || '',
  });
  const [categories, setCategories] = useState([]);

  useEffect(() => {
    categoryService.list().then(res => setCategories(res.data));
  }, []);

  const filtered = categories.filter(c => c.type === form.type);

  const set = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = (e) => {
    e.preventDefault();
    onSave({ ...form, amount: parseFloat(form.amount) });
  };

  const overlay = { position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 };
  const box = { background: '#fff', borderRadius: 12, padding: '2rem', width: 480, maxWidth: '95vw' };

  return (
    <div style={overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={box}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
          <h3 style={{ margin: 0 }}>{initial ? 'Editar' : 'Nova'} Transação</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer' }}>✕</button>
        </div>
        <form onSubmit={handleSubmit}>
          {/* Tipo */}
          <div style={{ display: 'flex', gap: 8, marginBottom: '1rem' }}>
            {['expense', 'income'].map(t => (
              <button key={t} type="button" onClick={() => set('type', t)}
                style={{ flex: 1, padding: '10px', borderRadius: 8, cursor: 'pointer', fontWeight: 600, border: '2px solid',
                  borderColor: form.type === t ? (t === 'income' ? '#22c55e' : '#ef4444') : '#e2e8f0',
                  background: form.type === t ? (t === 'income' ? '#f0fdf4' : '#fef2f2') : '#fff',
                  color: form.type === t ? (t === 'income' ? '#22c55e' : '#ef4444') : '#64748b' }}>
                {t === 'income' ? '+ Receita' : '- Despesa'}
              </button>
            ))}
          </div>

          {/* Campos */}
          {[
            { label: 'Descrição', key: 'description', type: 'text', required: true },
            { label: 'Valor (R$)', key: 'amount', type: 'number', required: true, step: '0.01', min: '0.01' },
            { label: 'Data', key: 'date', type: 'date', required: true },
          ].map(({ label, key, ...props }) => (
            <div key={key} style={{ marginBottom: '1rem' }}>
              <label style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}>{label}</label>
              <input {...props} value={form[key]} onChange={e => set(key, e.target.value)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: '1rem' }} />
            </div>
          ))}

          {/* Categoria */}
          <div style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}>Categoria</label>
            <select value={form.category_id} onChange={e => set('category_id', e.target.value)}
              style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: '1rem' }}>
              <option value="">Sem categoria</option>
              {filtered.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
            </select>
          </div>

          <button type="submit"
            style={{ width: '100%', padding: '12px', background: '#6366f1', color: '#fff', border: 'none', borderRadius: 8, cursor: 'pointer', fontWeight: 700, fontSize: '1rem' }}>
            {initial ? 'Salvar alterações' : 'Adicionar'}
          </button>
        </form>
      </div>
    </div>
  );
}
