import React, { useState, useEffect, useCallback } from 'react';
import { merchantService, categoryService } from '../services/api';
import Header, { HEADER_HEIGHT } from '../components/Header';

const fmt = (v) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

// A API serializa datas como "MM/dd/yyyy HH:mm:ss" (padrão .NET); convertemos para dd/MM/yyyy
const fmtDate = (d) => {
  if (!d) return '';
  const [datePart] = d.split(' ');
  const [month, day, year] = datePart.split('/');
  if (!month || !day || !year) return d;
  return `${day}/${month}/${year}`;
};

// ── Estilos (mesmo padrão visual das outras páginas) ────────────────────────
const s = {
  page:   { flex: 1, background: 'var(--bg-page)', fontFamily: "'Segoe UI', system-ui, sans-serif", padding: '2rem 1.5rem', paddingTop: `calc(2rem + ${HEADER_HEIGHT}px)` },
  card:   { background: 'var(--bg-card)', borderRadius: 14, boxShadow: '0 1px 6px rgba(0,0,0,0.07)', padding: '1.5rem', marginBottom: '1.5rem' },
  btn:    (bg = '#6366f1', color = '#fff', extra = {}) => ({
    background: bg, color, border: 'none', borderRadius: 8,
    padding: '8px 16px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem', ...extra,
  }),
  input:  { padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border)', fontSize: '0.875rem', boxSizing: 'border-box', background: 'var(--input-bg)', color: 'var(--text-primary)' },
  select: { padding: '7px 10px', borderRadius: 7, border: '1px solid var(--border)', fontSize: '0.85rem', background: 'var(--input-bg)', color: 'var(--text-secondary)' },
  badge:  (color = '#6366f1') => ({
    display: 'inline-flex', alignItems: 'center', gap: 4,
    background: color + '22', color, borderRadius: 20, padding: '2px 10px', fontSize: '0.75rem', fontWeight: 600,
  }),
  h2:     { fontSize: '1.1rem', fontWeight: 700, color: 'var(--text-primary)', margin: '0 0 1rem' },
  label:  { fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: 4 },
};

const confidenceColor = (c) => {
  if (c === null || c === undefined) return '#94a3b8';
  if (c >= 0.85) return '#22c55e';
  if (c >= 0.5) return '#f59e0b';
  return '#ef4444';
};

// ══════════════════════════════════════════════════════════════════════════
// Linha da fila de revisão — resolve manualmente um lançamento
// ══════════════════════════════════════════════════════════════════════════
function ReviewRow({ item, merchants, onResolved }) {
  const suggestedMerchant = merchants.find(m => m.name === item.suggested_name);
  const [choice, setChoice]   = useState(suggestedMerchant ? String(suggestedMerchant.id) : '__new__');
  const [newName, setNewName] = useState(item.suggested_name || '');
  const [loading, setLoading] = useState(false);
  const [error, setError]     = useState('');

  const resolve = async () => {
    if (choice === '__new__' && !newName.trim()) {
      setError('Informe um nome para o novo comerciante.');
      return;
    }
    setLoading(true); setError('');
    try {
      const payload = choice === '__new__'
        ? { review_id: item.id, new_name: newName.trim() }
        : { review_id: item.id, merchant_id: Number(choice) };
      await merchantService.resolve(payload);
      onResolved(item.id, choice === '__new__' ? newName.trim() : null);
    } catch (e) {
      setError(e.response?.data?.error || 'Erro ao resolver.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <tr style={{ borderBottom: '1px solid var(--border-light)' }}>
      <td style={{ padding: '8px 12px', maxWidth: 220 }}>
        <div style={{ fontWeight: 600, color: 'var(--text-primary)', fontSize: '0.82rem', wordBreak: 'break-word' }}>{item.clean_name}</div>
        <div style={{ color: 'var(--text-faint)', fontSize: '0.72rem', wordBreak: 'break-word' }}>{item.raw_name}</div>
      </td>
      <td style={{ padding: '8px 12px', whiteSpace: 'nowrap' }}>{fmtDate(item.transaction_date)}</td>
      <td style={{ padding: '8px 12px', whiteSpace: 'nowrap', fontWeight: 600 }}>{fmt(item.transaction_amount)}</td>
      <td style={{ padding: '8px 12px' }}>
        {item.confidence !== null && item.confidence !== undefined ? (
          <span style={s.badge(confidenceColor(item.confidence))}>
            {Math.round(item.confidence * 100)}% {item.suggested_name ? `· ${item.suggested_name}` : ''}
          </span>
        ) : (
          <span style={{ color: 'var(--text-faint)', fontSize: '0.78rem' }}>sem sugestão</span>
        )}
      </td>
      <td style={{ padding: '8px 12px', minWidth: 220 }}>
        <div style={{ display: 'flex', gap: 6 }}>
          <select value={choice} onChange={e => setChoice(e.target.value)} style={{ ...s.select, flex: 1 }}>
            <option value="__new__">+ Novo comerciante</option>
            {merchants.map(m => (
              <option key={m.id} value={m.id}>{m.name}</option>
            ))}
          </select>
        </div>
        {choice === '__new__' && (
          <input
            style={{ ...s.input, width: '100%', marginTop: 6 }}
            placeholder="Nome do comerciante"
            value={newName}
            onChange={e => setNewName(e.target.value)}
          />
        )}
        {error && <p style={{ color: '#ef4444', fontSize: '0.75rem', margin: '4px 0 0' }}>{error}</p>}
      </td>
      <td style={{ padding: '8px 12px' }}>
        <button onClick={resolve} disabled={loading} style={s.btn()}>
          {loading ? '...' : '✓ Confirmar'}
        </button>
      </td>
    </tr>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Aba: Fila de revisão
// ══════════════════════════════════════════════════════════════════════════
function ReviewQueueTab({ merchants, onMerchantsChange }) {
  const [items, setItems]           = useState([]);
  const [loading, setLoading]       = useState(true);
  const [backfilling, setBackfilling] = useState(false);
  const [backfillMsg, setBackfillMsg] = useState('');

  const load = useCallback(async () => {
    setLoading(true);
    try {
      const res = await merchantService.reviewQueue();
      setItems(res.data);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { load(); }, [load]);

  const runBackfill = async () => {
    setBackfilling(true); setBackfillMsg('');
    try {
      const res = await merchantService.backfill();
      setBackfillMsg(res.data.message);
      await load();
    } catch (e) {
      setBackfillMsg(e.response?.data?.error || 'Erro ao processar histórico.');
    } finally {
      setBackfilling(false);
    }
  };

  const handleResolved = async (reviewId, createdMerchantName) => {
    setItems(prev => prev.filter(i => i.id !== reviewId));
    if (createdMerchantName) {
      const res = await merchantService.list();
      onMerchantsChange(res.data);
    }
  };

  return (
    <div style={s.card}>
      <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem', flexWrap: 'wrap', gap: 8 }}>
        <div>
          <h3 style={{ ...s.h2, marginBottom: '0.4rem' }}>🔍 Fila de Revisão</h3>
          <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', margin: 0 }}>
            Lançamentos que o ML ainda não conseguiu resolver com confiança suficiente (≥ 85%).
            Confirme o comerciante correto — cada confirmação vira dado de treino.
          </p>
        </div>
        <button onClick={runBackfill} disabled={backfilling} style={s.btn('#818cf8')}>
          {backfilling ? 'Processando...' : '⚙️ Processar histórico sem comerciante'}
        </button>
      </div>

      {backfillMsg && (
        <div style={{ background: '#eef2ff', color: '#4338ca', borderRadius: 8, padding: '8px 12px', fontSize: '0.82rem', marginBottom: '1rem' }}>
          {backfillMsg}
        </div>
      )}

      {loading ? (
        <p style={{ color: 'var(--text-faint)' }}>Carregando...</p>
      ) : items.length === 0 ? (
        <div style={{ textAlign: 'center', padding: '2rem', color: 'var(--text-faint)' }}>
          <div style={{ fontSize: '2rem', marginBottom: 8 }}>✅</div>
          <p>Nenhum lançamento pendente de revisão.</p>
        </div>
      ) : (
        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ background: 'var(--bg-subtle)', borderBottom: '2px solid var(--border)' }}>
                {['Lançamento', 'Data', 'Valor', 'Sugestão ML', 'Resolver', ''].map(h => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {items.map(item => (
                <ReviewRow key={item.id} item={item} merchants={merchants} onResolved={handleResolved} />
              ))}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Aba: Comerciantes (CRUD + aliases manuais)
// ══════════════════════════════════════════════════════════════════════════
function MerchantsTab({ merchants, onMerchantsChange }) {
  const [categories, setCategories] = useState([]);
  const [newName, setNewName]       = useState('');
  const [newCategory, setNewCategory] = useState('');
  const [creating, setCreating]     = useState(false);
  const [aliasInputs, setAliasInputs] = useState({}); // merchantId -> texto
  const [addingAlias, setAddingAlias] = useState(null);
  const [savingCategory, setSavingCategory] = useState(null);

  useEffect(() => {
    categoryService.list().then(r => setCategories(r.data));
  }, []);

  const create = async () => {
    if (!newName.trim()) return;
    setCreating(true);
    try {
      await merchantService.create({ name: newName.trim(), category_id: newCategory ? Number(newCategory) : null });
      const res = await merchantService.list();
      onMerchantsChange(res.data);
      setNewName(''); setNewCategory('');
    } finally {
      setCreating(false);
    }
  };

  const addAlias = async (merchantId) => {
    const raw = aliasInputs[merchantId]?.trim();
    if (!raw) return;
    setAddingAlias(merchantId);
    try {
      await merchantService.addAlias(merchantId, raw);
      setAliasInputs(prev => ({ ...prev, [merchantId]: '' }));
    } finally {
      setAddingAlias(null);
    }
  };

  const changeCategory = async (merchantId, categoryId) => {
    setSavingCategory(merchantId);
    try {
      await merchantService.updateCategory(merchantId, categoryId ? Number(categoryId) : null);
      onMerchantsChange(prev => prev.map(m => m.id === merchantId ? { ...m, category_id: categoryId ? Number(categoryId) : null } : m));
    } finally {
      setSavingCategory(null);
    }
  };

  const expenseCategories = categories.filter(c => c.type === 'expense');

  return (
    <>
      <div style={s.card}>
        <h3 style={s.h2}>🏬 Novo Comerciante</h3>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          <input
            style={{ ...s.input, flex: 1, minWidth: 200 }}
            placeholder="Ex: Oliveira Mini, Shopee..."
            value={newName}
            onChange={e => setNewName(e.target.value)}
            onKeyDown={e => e.key === 'Enter' && create()}
          />
          <select style={s.select} value={newCategory} onChange={e => setNewCategory(e.target.value)}>
            <option value="">Sem categoria padrão</option>
            {expenseCategories.map(c => (
              <option key={c.id} value={c.id}>{c.name}</option>
            ))}
          </select>
          <button onClick={create} disabled={creating} style={s.btn()}>
            {creating ? '...' : '+ Criar'}
          </button>
        </div>
      </div>

      <div style={s.card}>
        <h3 style={s.h2}>📋 Comerciantes Cadastrados ({merchants.length})</h3>
        {merchants.length === 0 ? (
          <p style={{ color: 'var(--text-faint)', fontSize: '0.85rem' }}>Nenhum comerciante ainda. Crie um acima ou resolva itens na fila de revisão.</p>
        ) : (
          <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
            {merchants.map(m => (
              <div key={m.id} style={{ display: 'flex', alignItems: 'center', gap: 8, flexWrap: 'wrap', padding: '8px 12px', background: 'var(--bg-subtle)', borderRadius: 8 }}>
                <strong style={{ color: 'var(--text-primary)', minWidth: 160 }}>{m.name}</strong>
                <select
                  style={{ ...s.select, width: 170, opacity: savingCategory === m.id ? 0.6 : 1 }}
                  value={m.category_id ?? ''}
                  disabled={savingCategory === m.id}
                  onChange={e => changeCategory(m.id, e.target.value)}
                >
                  <option value="">Sem categoria</option>
                  {expenseCategories.map(c => (
                    <option key={c.id} value={c.id}>{c.name}</option>
                  ))}
                </select>
                <input
                  style={{ ...s.input, flex: 1, minWidth: 180 }}
                  placeholder="Adicionar alias (nome bruto do extrato)"
                  value={aliasInputs[m.id] || ''}
                  onChange={e => setAliasInputs(prev => ({ ...prev, [m.id]: e.target.value }))}
                  onKeyDown={e => e.key === 'Enter' && addAlias(m.id)}
                />
                <button onClick={() => addAlias(m.id)} disabled={addingAlias === m.id} style={s.btn('var(--bg-page)', 'var(--text-muted)')}>
                  {addingAlias === m.id ? '...' : '+ Alias'}
                </button>
              </div>
            ))}
          </div>
        )}
      </div>
    </>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Página principal
// ══════════════════════════════════════════════════════════════════════════
export default function MerchantsPage() {
  const [tab, setTab] = useState('review');
  const [merchants, setMerchants] = useState([]);

  useEffect(() => {
    merchantService.list().then(r => setMerchants(r.data));
  }, []);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Header showBack />
      <div style={s.page}>
        <div style={{ maxWidth: 1100, margin: '0 auto' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
            <h1 style={{ margin: 0, fontSize: '1.4rem', color: 'var(--text-primary)' }}>🏪 Comerciantes</h1>
            <div style={{ display: 'flex', gap: 8 }}>
              {[['review', '🔍 Fila de Revisão'], ['merchants', '🏬 Comerciantes']].map(([t, label]) => (
                <button key={t} onClick={() => setTab(t)}
                  style={s.btn(tab === t ? '#6366f1' : 'var(--bg-subtle)', tab === t ? '#fff' : 'var(--text-muted)')}>
                  {label}
                </button>
              ))}
            </div>
          </div>

          {tab === 'review' && <ReviewQueueTab merchants={merchants} onMerchantsChange={setMerchants} />}
          {tab === 'merchants' && <MerchantsTab merchants={merchants} onMerchantsChange={setMerchants} />}
        </div>
      </div>
    </div>
  );
}
