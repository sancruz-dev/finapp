import React, { useState, useEffect } from 'react';
import dayjs from 'dayjs';
import { categoryService } from '../services/api';

const fieldStyle = { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border)', fontSize: '0.95rem', boxSizing: 'border-box', background: 'var(--input-bg)', color: 'var(--text-primary)' };
const labelStyle = { display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.8rem', color: 'var(--text-muted)' };

function Field({ label, hint, children }) {
  return (
    <div>
      <label style={labelStyle}>
        {label}
        {hint && <span style={{ fontWeight: 400, color: 'var(--text-faint)' }}> {hint}</span>}
      </label>
      {children}
    </div>
  );
}

function Toggle({ id, checked, onChange, title, subtitle }) {
  return (
    <div style={{ display: 'flex', alignItems: 'center', justifyContent: 'space-between', gap: 10, padding: '9px 12px', borderRadius: 8, background: 'var(--bg-subtle)' }}>
      <label htmlFor={id} style={{ fontWeight: 500, fontSize: '0.82rem', cursor: 'pointer' }}>
        {title}
        {subtitle && (
          <div style={{ fontWeight: 400, fontSize: '0.72rem', color: 'var(--text-faint)', marginTop: 2 }}>
            {subtitle}
          </div>
        )}
      </label>
      <button
        id={id}
        type="button"
        role="switch"
        aria-checked={checked}
        onClick={() => onChange(!checked)}
        style={{
          flexShrink: 0, width: 38, height: 21, borderRadius: 999, border: 'none', cursor: 'pointer',
          background: checked ? '#6366f1' : 'var(--border)',
          position: 'relative', transition: 'background 0.15s',
        }}
      >
        <span style={{
          position: 'absolute', top: 2, left: checked ? 19 : 2,
          width: 17, height: 17, borderRadius: '50%', background: '#fff',
          transition: 'left 0.15s', boxShadow: '0 1px 2px rgba(0,0,0,0.25)',
        }} />
      </button>
    </div>
  );
}

export default function TransactionModal({ initial, onSave, onClose }) {
  const [form, setForm] = useState({
    type: 'expense',
    amount: '',
    description: '',
    date: dayjs().format('YYYY-MM-DD'),
    category_id: '',
    method: 'credito',
    installment: '',
    late_processing: false,
    fixed: false,
    subcategory_id: '',
    details: '',
    notes: '',
    ...initial,
    amount: initial?.amount != null ? String(initial.amount) : '',  // amount separado para garantir que string vazia funcione no input
    date: initial?.date // data: garante formato YYYY-MM-DD (vinda do banco pode ter T00:00:00)
      ? dayjs(initial.date).format('YYYY-MM-DD')
      : dayjs().format('YYYY-MM-DD'),
    installment: initial?.installment || '',  // evita null em input controlado
    late_processing: initial?.late_processing ?? false,
    fixed: initial?.fixed ?? false,
    subcategory_id: initial?.subcategory_id ?? '',
    details: initial?.details || '',
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
    onSave({
      ...form,
      amount: parseFloat(form.amount),
      category_id: form.category_id || null,
      installment: form.installment || null,
      late_processing: !!form.late_processing,
      fixed: !!form.fixed,
      subcategory_id: form.subcategory_id || null,
      details: form.details || null,
    });
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
  const box = { background: 'var(--bg-card)', color: 'var(--text-primary)', borderRadius: 12, padding: '1.75rem', width: 620, maxWidth: '95vw', maxHeight: '90vh', overflowY: 'auto' };
  const row = { display: 'grid', gap: 12, marginBottom: '1rem' };

  return (
    <div style={overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={box}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h3 style={{ margin: 0 }}>{initial ? 'Editar' : 'Nova'} Transação</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer', color: 'var(--text-primary)' }}>✕</button>
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
                    borderColor: active ? cfg.border : 'var(--border)',
                    background:  active ? cfg.bg    : 'var(--bg-card)',
                    color:       active ? cfg.color : 'var(--text-muted)',
                  }}>
                  {cfg.label}
                </button>
              );
            })}
          </div>

          {/* Descrição — linha própria (campo mais importante) */}
          <div style={row}>
            <Field label="Descrição">
              <input type="text" required value={form.description}
                onChange={e => set('description', e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          {/* Valor + Data */}
          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Field label="Valor (R$)">
              <input type="number" required step="0.01" min="0.01" value={form.amount}
                onChange={e => set('amount', e.target.value)} style={fieldStyle} />
            </Field>
            <Field label="Data">
              <input type="date" required value={form.date}
                onChange={e => set('date', e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          {/* Método + Parcela */}
          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Field label="Método de pagamento">
              <select value={form.method} onChange={e => set('method', e.target.value)} style={fieldStyle}>
                <option value="credito">Crédito</option>
                <option value="debito">Débito</option>
                <option value="pix">PIX</option>
                <option value="vr">VR (Vale Alimentação/Refeição)</option>
                <option value="cedula">Cédula</option>
              </select>
            </Field>
            <Field label="Parcela" hint="(opcional)">
              <input type="text" placeholder="Ex: Parcela 3/12" value={form.installment}
                onChange={e => set('installment', e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          {/* Categoria + Subcategoria */}
          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Field label="Categoria" hint={form.type === 'refund' ? '(da despesa reembolsada)' : null}>
              <select value={form.category_id} onChange={e => set('category_id', e.target.value)} style={fieldStyle}>
                <option value="">Sem categoria</option>
                {filtered.map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </Field>
            <Field label="Subcategoria" hint="(opcional)">
              <select value={form.subcategory_id} onChange={e => set('subcategory_id', e.target.value)} style={fieldStyle}>
                <option value="">Sem subcategoria</option>
                {filtered.filter(c => String(c.id) !== String(form.category_id)).map(c => <option key={c.id} value={c.id}>{c.name}</option>)}
              </select>
            </Field>
          </div>

          {/* Toggles — processamento tardio + transação fixa */}
          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Toggle
              id="late-processing-toggle"
              checked={form.late_processing}
              onChange={v => set('late_processing', v)}
              title="⏱ Processamento tardio"
              subtitle="Processado só no mês seguinte"
            />
            <Toggle
              id="fixed-toggle"
              checked={form.fixed}
              onChange={v => set('fixed', v)}
              title="📌 Transação fixa"
              subtitle="Aluguel, assinaturas, etc."
            />
          </div>

          {/* Detalhes */}
          <div style={row}>
            <Field label="Detalhes" hint="(opcional)">
              <textarea rows={2} placeholder="Ex: remédio e chocolate"
                value={form.details} onChange={e => set('details', e.target.value)}
                style={{ ...fieldStyle, fontFamily: 'inherit', resize: 'vertical' }} />
            </Field>
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
