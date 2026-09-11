import React, { useState } from 'react';
import dayjs from 'dayjs';

const fieldStyle = { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border)', fontSize: '0.95rem', boxSizing: 'border-box', background: 'var(--input-bg)', color: 'var(--text-primary)' };
const labelStyle = { display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.8rem', color: 'var(--text-muted)' };

function Field({ label, children }) {
  return (
    <div>
      <label style={labelStyle}>{label}</label>
      {children}
    </div>
  );
}

const TYPE_CONFIG = {
  APORTE:  { label: '+ Aporte',  border: '#22c55e', bg: '#f0fdf4', color: '#22c55e' },
  RESGATE: { label: '− Resgate', border: '#ef4444', bg: '#fef2f2', color: '#ef4444' },
};

export default function InvestmentMovementModal({ investment, onSave, onClose }) {
  const [type, setType] = useState('APORTE');
  const [amount, setAmount] = useState('');
  const [date, setDate] = useState(dayjs().format('YYYY-MM-DD'));
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState('');

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await onSave({ type, amount: parseFloat(amount), date });
      onClose();
    } catch (err) {
      setError(err.response?.data?.error || 'Erro ao registrar a movimentação.');
    } finally {
      setLoading(false);
    }
  };

  const overlay = {
    position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)',
    display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000,
  };
  const box = { background: 'var(--bg-card)', color: 'var(--text-primary)', borderRadius: 12, padding: '1.75rem', width: 420, maxWidth: '95vw' };
  const row = { display: 'grid', gap: 12, marginBottom: '1rem' };

  return (
    <div style={overlay} onClick={e => e.target === e.currentTarget && onClose()}>
      <div style={box}>
        <div style={{ display: 'flex', justifyContent: 'space-between', marginBottom: '0.25rem' }}>
          <h3 style={{ margin: 0 }}>Aporte / Resgate</h3>
          <button onClick={onClose} style={{ background: 'none', border: 'none', fontSize: '1.25rem', cursor: 'pointer', color: 'var(--text-primary)' }}>✕</button>
        </div>
        <p style={{ margin: '0 0 1.25rem', fontSize: '0.85rem', color: 'var(--text-faint)' }}>{investment.institution}</p>

        <form onSubmit={handleSubmit}>
          {/* Tipo — aba aporte/resgate */}
          <div style={{ display: 'flex', gap: 8, marginBottom: '1rem' }}>
            {['APORTE', 'RESGATE'].map(t => {
              const cfg = TYPE_CONFIG[t];
              const active = type === t;
              return (
                <button key={t} type="button" onClick={() => setType(t)}
                  style={{
                    flex: 1, padding: '10px', borderRadius: 8, cursor: 'pointer',
                    fontWeight: 600, border: '2px solid', fontSize: '0.85rem',
                    borderColor: active ? cfg.border : 'var(--border)',
                    background:  active ? cfg.bg    : 'var(--bg-card)',
                    color:       active ? cfg.color : 'var(--text-muted)',
                  }}>
                  {cfg.label}
                </button>
              );
            })}
          </div>

          <div style={row}>
            <Field label="Valor (R$)">
              <input type="number" required step="0.01" min="0.01" value={amount}
                onChange={e => setAmount(e.target.value)} style={fieldStyle} autoFocus />
            </Field>
          </div>

          <div style={row}>
            <Field label="Data">
              <input type="date" required value={date}
                onChange={e => setDate(e.target.value)} style={fieldStyle} />
            </Field>
          </div>

          {error && (
            <p style={{ margin: '0 0 1rem', padding: '8px 12px', borderRadius: 6, background: '#fef2f2', color: '#ef4444', fontSize: '0.82rem' }}>
              {error}
            </p>
          )}

          <button type="submit" disabled={loading}
            style={{ width: '100%', padding: '12px', background: TYPE_CONFIG[type].color, color: '#fff', border: 'none', borderRadius: 8, cursor: loading ? 'default' : 'pointer', fontWeight: 700, fontSize: '1rem', opacity: loading ? 0.7 : 1 }}>
            {loading ? 'Salvando…' : `Confirmar ${type === 'APORTE' ? 'aporte' : 'resgate'}`}
          </button>
        </form>
      </div>
    </div>
  );
}
