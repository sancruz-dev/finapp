import React, { useState } from 'react';
import dayjs from 'dayjs';

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

const ASSET_TYPES = [
  { value: 'CDB', label: 'CDB' },
  { value: 'LCI', label: 'LCI' },
  { value: 'LCA', label: 'LCA' },
  { value: 'TESOURO', label: 'Tesouro Direto' },
  { value: 'POUPANCA', label: 'Poupança' },
];

const INDEXERS = [
  { value: 'CDI', label: '% do CDI' },
  { value: 'SELIC', label: '% da Selic' },
  { value: 'PREFIXADO', label: 'Prefixado (taxa fixa a.a.)' },
];

export default function InvestmentModal({ initial, onSave, onClose }) {
  const [form, setForm] = useState({
    institution: '',
    asset_type: 'CDB',
    indexer: 'CDI',
    indexer_rate: '100',
    principal_amount: '',
    applied_at: dayjs().format('YYYY-MM-DD'),
    maturity_at: '',
    ...initial,
    principal_amount: initial?.principal_amount != null ? String(initial.principal_amount) : '',
    indexer_rate: initial?.indexer_rate != null ? String(initial.indexer_rate) : '100',
    applied_at: initial?.applied_at ? dayjs(initial.applied_at).format('YYYY-MM-DD') : dayjs().format('YYYY-MM-DD'),
    maturity_at: initial?.maturity_at ? dayjs(initial.maturity_at).format('YYYY-MM-DD') : '',
  });

  const isPoupanca = form.asset_type === 'POUPANCA';

  // Poupança não tem % configurável pelo usuário — troca de/para o tipo já ajusta o indexador.
  const set = (k, v) => setForm(f => {
    const next = { ...f, [k]: v };
    if (k === 'asset_type') {
      next.indexer = v === 'POUPANCA' ? 'POUPANCA' : (f.indexer === 'POUPANCA' ? 'CDI' : f.indexer);
    }
    return next;
  });

  const handleSubmit = (e) => {
    e.preventDefault();
    onSave({
      ...form,
      principal_amount: parseFloat(form.principal_amount),
      indexer_rate: isPoupanca ? null : parseFloat(form.indexer_rate),
      maturity_at: form.maturity_at || null,
    });
  };

  const overlay = {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
    display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
  };
  const box = { background: 'var(--bg-card)', color: 'var(--text-primary)', borderRadius: 12, padding: '1.75rem', width: 560, maxWidth: '95vw', maxHeight: '90vh', overflowY: 'auto' };
  const row = { display: 'grid', gap: 12, marginBottom: '1rem' };

  return (
    <div style={overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={box}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '1.25rem' }}>
          <h3 style={{ margin: 0 }}>{initial ? 'Editar' : 'Novo'} Investimento</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer', color: 'var(--text-primary)' }}>✕</button>
        </div>

        <form onSubmit={handleSubmit}>
          <div style={row}>
            <Field label="Instituição">
              <input type="text" required placeholder="Ex: Banco Inter" value={form.institution}
                onChange={e => set('institution', e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Field label="Tipo de ativo">
              <select value={form.asset_type} onChange={e => set('asset_type', e.target.value)} style={fieldStyle}>
                {ASSET_TYPES.map(t => <option key={t.value} value={t.value}>{t.label}</option>)}
              </select>
            </Field>
            <Field label="Indexador">
              {isPoupanca ? (
                <input type="text" disabled value="Regra da poupança" style={{ ...fieldStyle, color: 'var(--text-faint)' }} />
              ) : (
                <select value={form.indexer} onChange={e => set('indexer', e.target.value)} style={fieldStyle}>
                  {INDEXERS.map(i => <option key={i.value} value={i.value}>{i.label}</option>)}
                </select>
              )}
            </Field>
          </div>

          {!isPoupanca && (
            <div style={row}>
              <Field label={form.indexer === 'PREFIXADO' ? 'Taxa (% ao ano)' : `Taxa (% do ${form.indexer === 'SELIC' ? 'Selic' : 'CDI'})`}>
                <input type="number" required step="0.01" min="0" value={form.indexer_rate}
                  onChange={e => set('indexer_rate', e.target.value)} style={fieldStyle} />
              </Field>
            </div>
          )}

          <div style={{ ...row, gridTemplateColumns: '1fr 1fr' }}>
            <Field label="Valor aplicado (R$)">
              <input type="number" required step="0.01" min="0.01" value={form.principal_amount}
                onChange={e => set('principal_amount', e.target.value)} style={fieldStyle} />
            </Field>
            <Field label="Data de aplicação">
              <input type="date" required value={form.applied_at}
                onChange={e => set('applied_at', e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          <div style={row}>
            <Field label="Vencimento" hint="(opcional)">
              <input type="date" value={form.maturity_at}
                onChange={e => set('maturity_at', e.target.value)} style={fieldStyle} />
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
