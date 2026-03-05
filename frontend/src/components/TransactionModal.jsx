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

  // Reembolso usa categorias de despesa (é um desconto de despesa)
  const filtered = categories.filter(c =>
    form.type === 'refund' ? c.type === 'expense' : c.type === form.type
  );

  const set = (k, v) => setForm(f => ({ ...f, [k]: v }));

  const handleSubmit = (e) => {
    e.preventDefault();
    onSave({ ...form, amount: parseFloat(form.amount) });
  };

  const typeConfig = {
    expense: { label: '- Despesa',    border: '#ef4444', bg: '#fef2f2', color: '#ef4444' },
    refund:  { label: '↩ Reembolso',  border: '#6366f1', bg: '#eef2ff', color: '#6366f1' },
    income:  { label: '+ Receita',    border: '#22c55e', bg: '#f0fdf4', color: '#22c55e' },
  };

  const overlay = {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
    display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
  };
  const box = { background: '#fff', borderRadius: 12, padding: '2rem', width: 480, maxWidth: '95vw' };

  return (
    <div style={overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={box}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.5rem' }}>
          <h3 style={{ margin: 0 }}>{initial ? 'Editar' : 'Nova'} Transação</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer' }}>✕</button>
        </div>

        <form onSubmit={handleSubmit}>
          {/* Tipo — três opções */}
          <div style={{ display: 'flex', gap: 8, marginBottom: '1rem' }}>
            {['expense', 'refund', 'income'].map(t => {
              const cfg = typeConfig[t];
              const active = form.type === t;
              return (
                <button key={t} type="button" onClick={() => set('type', t)}
                  style={{
                    flex: 1, padding: '10px', borderRadius: 8, cursor: 'pointer',
                    fontWeight: 600, border: '2px solid', fontSize: '0.82rem',
                    borderColor: active ? cfg.border : '#e2e8f0',
                    background:  active ? cfg.bg    : '#fff',
                    color:       active ? cfg.color : '#64748b',
                  }}>
                  {cfg.label}
                </button>
              );
            })}
          </div>

          {/* Campos */}
          {[
            { label: 'Descrição', key: 'description', type: 'text',   required: true },
            { label: 'Valor (R$)', key: 'amount',     type: 'number', required: true, step: '0.01', min: '0.01' },
            { label: 'Data',       key: 'date',        type: 'date',   required: true },
          ].map(({ label, key, ...props }) => (
            <div key={key} style={{ marginBottom: '1rem' }}>
              <label style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}>{label}</label>
              <input {...props} value={form[key]} onChange={e => set(key, e.target.value)}
                style={{ width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid #e2e8f0', fontSize: '1rem', boxSizing: 'border-box' }} />
            </div>
          ))}

          {/* Categoria */}
          <div style={{ marginBottom: '1rem' }}>
            <label style={{ display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem' }}>
              Categoria
              {form.type === 'refund' && (
                <span style={{ marginLeft: 6, fontSize: '0.75rem', color: '#6366f1', fontWeight: 400 }}>
                  (categoria da despesa reembolsada)
                </span>
              )}
            </label>
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