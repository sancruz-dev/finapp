import React, { useState, useEffect, useRef } from 'react';
import axios from 'axios';
import Header, { HEADER_HEIGHT } from '../components/Header';

// ── API helpers ────────────────────────────────────────────────────────────
const api = axios.create({ baseURL: process.env.REACT_APP_API_URL || '/api' });
api.interceptors.request.use(cfg => {
  const t = localStorage.getItem('token');
  if (t) cfg.headers.Authorization = `Bearer ${t}`;
  return cfg;
});

const fmt = v =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

// ── Estilos ────────────────────────────────────────────────────────────────
const s = {
  page:   { flex: 1, background: 'var(--bg-page)', fontFamily: "'Segoe UI', system-ui, sans-serif", padding: '2rem 1.5rem', paddingTop: `calc(2rem + ${HEADER_HEIGHT}px)` },
  card:   { background: 'var(--bg-card)', borderRadius: 14, boxShadow: '0 1px 6px rgba(0,0,0,0.07)', padding: '1.5rem', marginBottom: '1.5rem' },
  btn:    (bg = '#6366f1', color = '#fff', extra = {}) => ({
    background: bg, color, border: 'none', borderRadius: 8,
    padding: '8px 16px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem', ...extra,
  }),
  input:  { padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border)', fontSize: '0.875rem', boxSizing: 'border-box', background: 'var(--input-bg)', color: 'var(--text-primary)' },
  badge:  (color = '#6366f1') => ({
    display: 'inline-flex', alignItems: 'center', gap: 4,
    background: color + '22', color, borderRadius: 20, padding: '2px 10px', fontSize: '0.75rem', fontWeight: 600,
  }),
  tag:    { display: 'inline-flex', alignItems: 'center', gap: 4, background: 'var(--bg-subtle)',
            borderRadius: 20, padding: '3px 10px', fontSize: '0.78rem', color: 'var(--text-secondary)' },
  select: { padding: '7px 10px', borderRadius: 7, border: '1px solid var(--border)', fontSize: '0.85rem', background: 'var(--input-bg)', color: 'var(--text-secondary)' },
  h2:     { fontSize: '1.1rem', fontWeight: 700, color: 'var(--text-primary)', margin: '0 0 1rem' },
  label:  { fontSize: '0.8rem', fontWeight: 600, color: 'var(--text-muted)', display: 'block', marginBottom: 4 },
};

// ══════════════════════════════════════════════════════════════════════════
// KeywordsManager
// ══════════════════════════════════════════════════════════════════════════
function KeywordsManager({ categories, onCategoriesChange }) {
  const [selected, setSelected] = useState(null);
  const [newKw, setNewKw]       = useState('');
  const [loading, setLoading]   = useState(false);

  const cat = categories.find(c => c.id === selected);

  const reload = async () => {
    const res = await api.get('/categories');
    onCategoriesChange(res.data);
  };

  const addKeyword = async () => {
    if (!newKw.trim() || !selected) return;
    setLoading(true);
    try {
      await api.post(`/categories/${selected}/keywords`, { keyword: newKw.trim() });
      await reload();
      setNewKw('');
    } finally { setLoading(false); }
  };

  const removeKeyword = async (kwId) => {
    await api.delete(`/categories/keywords/${kwId}`);
    await reload();
  };

  return (
    <div style={s.card}>
      <h3 style={s.h2}>🏷️ Palavras-chave por Categoria</h3>
      <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', marginBottom: '1.25rem' }}>
        Palavras-chave são usadas para categorizar automaticamente os lançamentos importados.
        A busca é <strong>case-insensitive</strong> — IFOOD, Ifood e ifood são equivalentes.
      </p>

      <div style={{ display: 'grid', gridTemplateColumns: '220px 1fr', gap: '1.5rem' }}>
        {/* Lista de categorias */}
        <div>
          <label style={s.label}>Selecione a categoria</label>
          <div style={{ display: 'flex', flexDirection: 'column', gap: 4 }}>
            {categories.map(c => (
              <button key={c.id} onClick={() => setSelected(c.id)} style={{
                display: 'flex', alignItems: 'center', gap: 8,
                padding: '8px 12px', borderRadius: 8, border: 'none', cursor: 'pointer',
                textAlign: 'left', fontSize: '0.875rem',
                background: selected === c.id ? c.color + '22' : 'transparent',
                color: selected === c.id ? c.color : 'var(--text-secondary)',
                fontWeight: selected === c.id ? 700 : 500,
              }}>
                <span style={{ width: 10, height: 10, borderRadius: '50%', background: c.color, flexShrink: 0 }} />
                {c.name}
                {c.keywords?.length > 0 && (
                  <span style={{ ...s.badge(c.color), marginLeft: 'auto', padding: '1px 7px' }}>
                    {c.keywords.length}
                  </span>
                )}

              </button>
            ))}
          </div>
        </div>

        {/* Keywords */}
        <div>
          {!cat ? (
            <div style={{ color: 'var(--text-faint)', fontSize: '0.875rem', marginTop: '2.5rem', textAlign: 'center' }}>
              ← Selecione uma categoria para gerenciar palavras-chave
            </div>
          ) : (
            <>
              <div style={{ display: 'flex', alignItems: 'center', gap: 8, marginBottom: '1rem' }}>
                <span style={{ width: 12, height: 12, borderRadius: '50%', background: cat.color }} />
                <strong style={{ color: 'var(--text-primary)' }}>{cat.name}</strong>
                <span style={s.badge(cat.type === 'income' ? '#22c55e' : '#ef4444')}>
                  {cat.type === 'income' ? 'Receita' : 'Despesa'}
                </span>
              </div>

              <div style={{ display: 'flex', gap: 8, marginBottom: '1rem' }}>
                <input
                  style={{ ...s.input, flex: 1 }}
                  placeholder="Ex: IFOOD, UBER, FARMACIA..."
                  value={newKw}
                  onChange={e => setNewKw(e.target.value)}
                  onKeyDown={e => e.key === 'Enter' && addKeyword()}
                />
                <button onClick={addKeyword} disabled={loading} style={s.btn()}>
                  + Adicionar
                </button>
              </div>

              {(!cat.keywords || cat.keywords.length === 0) ? (
                <p style={{ color: 'var(--text-faint)', fontSize: '0.85rem' }}>Nenhuma palavra-chave cadastrada.</p>
              ) : (
                <div style={{ display: 'flex', flexWrap: 'wrap', gap: 6 }}>
                  {cat.keywords.map((kw) => (
                    <span key={kw.id} style={s.tag}>
                      {kw.keyword}
                      <button
                        onClick={() => removeKeyword(kw.id)}
                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: 'var(--text-faint)', padding: 0, lineHeight: 1 }}
                      >✕</button>
                    </span>
                  ))}
                </div>
              )}
            </>
          )}
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// CsvUploader
// ══════════════════════════════════════════════════════════════════════════
function CsvUploader({ onPreview }) {
  const [dragging, setDragging] = useState(false);
  const [loading, setLoading]   = useState(false);
  const [error, setError]       = useState('');
  const inputRef                = useRef();

  const upload = async (file) => {
    if (!file) return;
    setLoading(true); setError('');
    try {
      const form = new FormData();
      form.append('file', file);
      const res = await api.post('/import/preview', form, {
        headers: { 'Content-Type': 'multipart/form-data' },
      });
      onPreview(res.data);
    } catch (e) {
      setError(e.response?.data?.error || 'Erro ao processar o arquivo.');
    } finally { setLoading(false); }
  };

  return (
    <div style={s.card}>
      <h3 style={s.h2}>📄 Importar Fatura CSV/XLSX</h3>
      <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', marginBottom: '1rem' }}>
        Formato esperado: <code style={{ background: 'var(--bg-subtle)', padding: '1px 6px', borderRadius: 4 }}>Data, Descrição, Valor</code> —
        separador vírgula ou ponto-e-vírgula. Valor negativo = despesa, positivo = receita.
      </p>

      <div
        onDragOver={e => { e.preventDefault(); setDragging(true); }}
        onDragLeave={() => setDragging(false)}
        onDrop={e => { e.preventDefault(); setDragging(false); upload(e.dataTransfer.files[0]); }}
        onClick={() => inputRef.current?.click()}
        style={{
          border: `2px dashed ${dragging ? '#6366f1' : 'var(--border)'}`,
          borderRadius: 12, padding: '3rem', textAlign: 'center', cursor: 'pointer',
          background: dragging ? 'var(--tint-accent-bg)' : 'var(--bg-subtle)', transition: 'all 0.2s',
        }}
      >
        <div style={{ fontSize: '2.5rem', marginBottom: 8 }}>📂</div>
        <p style={{ color: 'var(--text-secondary)', fontWeight: 600, margin: 0 }}>
          {loading ? 'Processando...' : 'Arraste o CSV/XLSX aqui ou clique para selecionar'}
        </p>
        <p style={{ color: 'var(--text-faint)', fontSize: '0.8rem', marginTop: 4 }}>Arquivos .csv ou .xlsx até 10 MB</p>
        <input ref={inputRef} type="file" accept=".csv,.xlsx,.xls,application/vnd.openxmlformats-officedocument.spreadsheetml.sheet,application/vnd.ms-excel" style={{ display: 'none' }}
          onChange={e => upload(e.target.files[0])} />
      </div>
      {error && <p style={{ color: '#ef4444', marginTop: '0.75rem', fontSize: '0.875rem' }}>{error}</p>}
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// ImportReview
// ══════════════════════════════════════════════════════════════════════════
function ImportReview({ preview, categories, onReset }) {
  const [rows, setRows]             = useState(preview.rows);
  const [loading, setLoading]       = useState(false);
  const [done, setDone]             = useState(null);
  const [showOnlyInstallments, setShowOnlyInstallments] = useState(false);

  const dominantMonth    = preview.dominant_month;
  const installmentCount = rows.filter(r => r.is_installment).length;

  const formatMonth = (ym) => {
    if (!ym) return '';
    const [y, m] = ym.split('-');
    const names = ['janeiro','fevereiro','março','abril','maio','junho',
                   'julho','agosto','setembro','outubro','novembro','dezembro'];
    return `${names[parseInt(m) - 1]}/${y}`;
  };

  const fixAllInstallments = () => {
    if (!dominantMonth) return;
    setRows(r => r.map(row => {
      if (!row.is_installment) return row;
      const originalDay = row.date.slice(8, 10);
      const [y, m] = dominantMonth.split('-');
      const lastDay = new Date(parseInt(y), parseInt(m), 0).getDate();
      const day = Math.min(parseInt(originalDay), lastDay).toString().padStart(2, '0');
      return { ...row, date: `${dominantMonth}-${day}`, is_installment: false };
    }));
    setShowOnlyInstallments(false);
  };

  const expenseCategories = categories.filter(c => c.type === 'expense');

  const setRowField = (idx, field, value) =>
    setRows(r => r.map((row, i) => i === idx ? { ...row, [field]: value } : row));

  const setCategoryForRow = (idx, catId) => {
    const cat = expenseCategories.find(c => c.id === Number(catId));
    setRows(r => r.map((row, i) => i === idx ? {
      ...row,
      category_id:    cat?.id    ?? null,
      category_name:  cat?.name  ?? null,
      category_color: cat?.color ?? null,
    } : row));
  };

  const confirm = async () => {
    setLoading(true);
    try {
      const res = await api.post('/import/confirm', { rows });
      setDone(res.data.saved);
    } finally { setLoading(false); }
  };

  if (done !== null) {
    return (
      <div style={{ ...s.card, textAlign: 'center', padding: '3rem' }}>
        <div style={{ fontSize: '3.5rem' }}>✅</div>
        <h2 style={{ color: '#22c55e', margin: '0.5rem 0' }}>{done} transações importadas!</h2>
        <p style={{ color: 'var(--text-muted)' }}>Acesse o dashboard para visualizar os lançamentos.</p>
        <button onClick={onReset} style={{ ...s.btn(), marginTop: '1rem' }}>
          Importar outro arquivo
        </button>
      </div>
    );
  }

  const unmatched    = rows.filter(r => !r.category_id).length;
  const displayRows  = showOnlyInstallments ? rows.filter(r => r.is_installment) : rows;
  const pendingInst  = rows.filter(r => r.is_installment).length;

  return (
    <div>
      {/* ── Banner de parcelas detectadas ─────────────────────────────── */}
      {installmentCount > 0 && (
        <div style={{
          background: 'var(--tint-amber-bg)', border: '1px solid #f59e0b', borderRadius: 12,
          padding: '1rem 1.25rem', marginBottom: '1rem',
          display: 'flex', alignItems: 'flex-start', gap: 12, flexWrap: 'wrap',
        }}>
          <span style={{ fontSize: '1.4rem', lineHeight: 1 }}>📅</span>
          <div style={{ flex: 1, minWidth: 200 }}>
            <p style={{ margin: 0, fontWeight: 700, color: 'var(--tint-amber-text)', fontSize: '0.9rem' }}>
              {installmentCount} lançamento{installmentCount > 1 ? 's' : ''} parcelado{installmentCount > 1 ? 's' : ''} detectado{installmentCount > 1 ? 's' : ''}
            </p>
            <p style={{ margin: '3px 0 0', color: 'var(--tint-amber-text-2)', fontSize: '0.82rem' }}>
              Estes lançamentos têm datas fora de <strong>{formatMonth(dominantMonth)}</strong> — provavelmente são parcelas.
              Corrija a data manualmente ou use <strong>"Corrigir todas"</strong> para mover para {formatMonth(dominantMonth)}.
            </p>
          </div>
          <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
            <button
              onClick={() => setShowOnlyInstallments(v => !v)}
              style={s.btn(showOnlyInstallments ? '#f59e0b' : 'var(--tint-amber-bg)', showOnlyInstallments ? '#fff' : 'var(--tint-amber-text)')}
            >
              {showOnlyInstallments ? '← Ver todas' : `🔍 Ver parcelas (${pendingInst})`}
            </button>
            {pendingInst > 0 && (
              <button onClick={fixAllInstallments} style={s.btn('#f59e0b', '#fff')}>
                ✓ Corrigir todas
              </button>
            )}
          </div>
        </div>
      )}

      <div style={s.card}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem', flexWrap: 'wrap', gap: 8 }}>
          <div>
            <h3 style={{ ...s.h2, marginBottom: '0.4rem' }}>
              🔍 Revisão — {rows.length} lançamentos
              {showOnlyInstallments && <span style={{ color: '#f59e0b', fontWeight: 400, fontSize: '0.85rem' }}> (filtrando parcelas)</span>}
            </h3>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <span style={s.badge('#22c55e')}>{rows.length - unmatched} categorizados</span>
              {unmatched > 0 && <span style={s.badge('#f97316')}>{unmatched} sem categoria</span>}
              {pendingInst > 0 && <span style={s.badge('#f59e0b')}>{pendingInst} parcelas pendentes</span>}
              {rows.filter(r => r.type === 'refund').length > 0 && (
                <span style={s.badge('#22c55e')}>↩ {rows.filter(r => r.type === 'refund').length} reembolso{rows.filter(r => r.type === 'refund').length > 1 ? 's' : ''}</span>
              )}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button onClick={onReset} style={s.btn('var(--bg-subtle)', 'var(--text-muted)')}>← Novo arquivo</button>
            <button onClick={confirm} disabled={loading} style={s.btn()}>
              {loading ? 'Salvando...' : `✓ Confirmar (${rows.length})`}
            </button>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ background: 'var(--bg-subtle)', borderBottom: '2px solid var(--border)' }}>
                {['Data', 'Descrição', 'Tipo', 'Valor (R$)', 'Categoria', ''].map(h => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {displayRows.map((row) => {
                const realIdx = rows.indexOf(row);
                return (
                  <tr key={realIdx} style={{
                    borderBottom: '1px solid var(--border-light)',
                    background: row.type === 'refund'
                      ? 'var(--tint-green-bg)'
                      : row.is_installment
                        ? 'var(--tint-orange-bg)'
                        : (!row.category_id ? 'var(--tint-amber-bg)' : 'transparent'),
                  }}>
                    {/* Data */}
                    <td style={{ padding: '8px 12px' }}>
                      <div style={{ display: 'flex', alignItems: 'center', gap: 6 }}>
                        {row.is_installment && (
                          <span title="Data fora do mês principal — possível parcela" style={{
                            fontSize: '0.68rem', background: '#f59e0b', color: '#fff',
                            borderRadius: 4, padding: '2px 5px', fontWeight: 700, whiteSpace: 'nowrap',
                          }}>PARCELA</span>
                        )}
                        <input type="date" value={row.date}
                          onChange={e => {
                            const newDate = e.target.value;
                            const isStillOutside = dominantMonth ? !newDate.startsWith(dominantMonth) : false;
                            setRows(r => r.map((ro, i) => i === realIdx
                              ? { ...ro, date: newDate, is_installment: isStillOutside }
                              : ro));
                          }}
                          style={{ ...s.input, width: 140, borderColor: row.is_installment ? '#f59e0b' : 'var(--border)' }}
                        />
                      </div>
                    </td>
                    {/* Descrição */}
                    <td style={{ padding: '8px 12px', minWidth: 200 }}>
                      <input value={row.description} onChange={e => setRowField(realIdx, 'description', e.target.value)}
                        style={{ ...s.input, width: '100%' }} />
                    </td>
                    {/* Tipo */}
                    <td style={{ padding: '8px 12px' }}>
                      <select
                        value={row.type}
                        onChange={e => setRowField(realIdx, 'type', e.target.value)}
                        style={{
                          ...s.select, width: 120,
                          color: row.type === 'refund' ? '#22c55e' : '#ef4444',
                          fontWeight: 700,
                          borderColor: row.type === 'refund' ? '#22c55e' : 'var(--border)',
                        }}
                      >
                        <option value="expense">Despesa</option>
                        <option value="refund">Reembolso</option>
                      </select>
                    </td>
                    {/* Valor */}
                    <td style={{ padding: '8px 12px' }}>
                      <input type="number" step="0.01" min="0" value={row.amount}
                        onChange={e => setRowField(realIdx, 'amount', parseFloat(e.target.value) || 0)}
                        style={{ ...s.input, width: 110, color: row.type === 'refund' ? '#22c55e' : '#ef4444', fontWeight: 700 }} />
                    </td>
                    {/* Categoria — despesas e reembolsos usam as mesmas categorias */}
                    <td style={{ padding: '8px 12px', minWidth: 170 }}>
                      <select
                        value={row.category_id ?? ''}
                        onChange={e => setCategoryForRow(realIdx, e.target.value)}
                        style={{ ...s.select, width: '100%', borderColor: !row.category_id ? '#f97316' : 'var(--border)', background: !row.category_id ? 'var(--tint-amber-bg)' : 'var(--input-bg)' }}
                      >
                        <option value="">⚠ Sem categoria</option>
                        {expenseCategories.map(c => (
                          <option key={c.id} value={c.id}>{c.name}</option>
                        ))}
                      </select>
                    </td>
                    {/* Remover */}
                    <td style={{ padding: '8px 12px' }}>
                      <button onClick={() => setRows(r => r.filter((_, i) => i !== realIdx))}
                        style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#ef4444', fontSize: '1.1rem' }}>🗑️</button>
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      </div>
    </div>
  );
}

// ══════════════════════════════════════════════════════════════════════════
// Página principal de importação
// ══════════════════════════════════════════════════════════════════════════
export default function ImportPage() {
  const [categories, setCategories] = useState([]);
  const [preview, setPreview]       = useState(null);
  const [tab, setTab]               = useState('import');

  useEffect(() => {
    api.get('/categories').then(r => setCategories(r.data));
  }, []);

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Header showBack />
      <div style={s.page}>
        <div style={{ maxWidth: 1100, margin: '0 auto' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
            <h1 style={{ margin: 0, fontSize: '1.4rem', color: 'var(--text-primary)' }}>📥 Importação de Fatura</h1>
            <div style={{ display: 'flex', gap: 8 }}>
              {[['import', '📄 Importação'], ['keywords', '🏷️ Palavras-chave']].map(([t, label]) => (
                <button key={t} onClick={() => setTab(t)}
                  style={s.btn(tab === t ? '#6366f1' : 'var(--bg-subtle)', tab === t ? '#fff' : 'var(--text-muted)')}>
                  {label}
                </button>
              ))}
            </div>
          </div>

          {tab === 'keywords' && (
            <KeywordsManager categories={categories} onCategoriesChange={setCategories} />
          )}

          {tab === 'import' && !preview && (
            <CsvUploader onPreview={setPreview} />
          )}

          {tab === 'import' && preview && (
            <ImportReview preview={preview} categories={categories} onReset={() => setPreview(null)} />
          )}
        </div>
      </div>
    </div>
  );
}