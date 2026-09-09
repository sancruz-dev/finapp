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
// CsvUploader
// ══════════════════════════════════════════════════════════════════════════
const IMPORT_TYPES = [
  { value: 'fatura',  label: '💳 Fatura (cartão de crédito)' },
  { value: 'extrato', label: '🏦 Extrato da conta' },
  { value: 'vr',      label: '🍽️ Extrato VR' },
];

function CsvUploader({ importType, onImportTypeChange, onPreview }) {
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
      form.append('importType', importType);
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
      <h3 style={s.h2}>📄 Importar CSV/XLSX</h3>

      <div style={{ marginBottom: '1.25rem' }}>
        <label style={s.label}>Tipo de importação</label>
        <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
          {IMPORT_TYPES.map(t => (
            <label key={t.value} style={{
              display: 'flex', alignItems: 'center', gap: 6, cursor: 'pointer',
              padding: '8px 14px', borderRadius: 8, fontSize: '0.85rem', fontWeight: 600,
              border: `1.5px solid ${importType === t.value ? '#6366f1' : 'var(--border)'}`,
              background: importType === t.value ? 'var(--tint-accent-bg)' : 'var(--bg-subtle)',
              color: importType === t.value ? '#6366f1' : 'var(--text-secondary)',
            }}>
              <input type="radio" name="importType" value={t.value}
                checked={importType === t.value}
                onChange={() => onImportTypeChange(t.value)}
                style={{ margin: 0 }} />
              {t.label}
            </label>
          ))}
        </div>
      </div>

      <p style={{ color: 'var(--text-muted)', fontSize: '0.85rem', marginBottom: '1rem' }}>
        Formato esperado: <code style={{ background: 'var(--bg-subtle)', padding: '1px 6px', borderRadius: 4 }}>Data, Descrição, Valor</code> —
        separador vírgula ou ponto-e-vírgula.{' '}
        {importType === 'fatura'
          ? 'Valor negativo = reembolso, positivo = despesa.'
          : 'Valor negativo = despesa, positivo = receita.'}
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
function ImportReview({ preview, categories, importType, onReset }) {
  const [rows, setRows]             = useState(preview.rows);
  const [loading, setLoading]       = useState(false);
  const [done, setDone]             = useState(null);
  const [duplicates, setDuplicates] = useState(null);

  const showInstallment = importType === 'fatura';
  const showMethod      = importType === 'extrato';
  const showIncome      = importType !== 'fatura';

  const installmentCount = rows.filter(r => r.installment).length;
  const incomeCount      = rows.filter(r => r.type === 'income').length;

  const expenseCategories = categories.filter(c => c.type === 'expense');
  const incomeCategories  = categories.filter(c => c.type === 'income');
  const categoriesForType = (type) => type === 'income' ? incomeCategories : expenseCategories;

  const setRowField = (idx, field, value) =>
    setRows(r => r.map((row, i) => i === idx ? { ...row, [field]: value } : row));

  const setCategoryForRow = (idx, catId) => {
    setRows(r => r.map((row, i) => {
      if (i !== idx) return row;
      const cat = categoriesForType(row.type).find(c => c.id === Number(catId));
      return {
        ...row,
        category_id:    cat?.id    ?? null,
        category_name:  cat?.name  ?? null,
        category_color: cat?.color ?? null,
      };
    }));
  };

  const setSubcategoryForRow = (idx, catId) => {
    setRows(r => r.map((row, i) => {
      if (i !== idx) return row;
      const cat = categoriesForType(row.type).find(c => c.id === Number(catId));
      return {
        ...row,
        subcategory_id:    cat?.id    ?? null,
        subcategory_name:  cat?.name  ?? null,
        subcategory_color: cat?.color ?? null,
      };
    }));
  };

  const confirm = async (force = false) => {
    setLoading(true);
    try {
      const res = await api.post('/import/confirm', { rows, force });
      setDone(res.data.saved);
      setDuplicates(null);
    } catch (e) {
      if (e.response?.status === 409 && e.response?.data?.duplicates) {
        setDuplicates(e.response.data.duplicates);
      } else {
        alert(e.response?.data?.error || 'Erro ao confirmar importação.');
      }
    } finally { setLoading(false); }
  };

  const removeDuplicatesAndRetry = async () => {
    setLoading(true);
    try {
      const ids = duplicates.flatMap(d => d.existing.map(ex => ex.id));
      await Promise.all(ids.map(id => api.delete(`/transactions/${id}`)));
      await confirm(true);
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

  const unmatched = rows.filter(r => !r.category_id).length;

  return (
    <div>
      {duplicates && (
        <div style={{ position: 'fixed', inset: 0, background: 'rgba(0,0,0,0.4)', display: 'flex', alignItems: 'center', justifyContent: 'center', zIndex: 1000 }}>
          <div style={{ background: 'var(--bg-card)', color: 'var(--text-primary)', borderRadius: 12, padding: '1.75rem', width: 520, maxWidth: '95vw', maxHeight: '85vh', overflowY: 'auto' }}>
            <h3 style={{ margin: '0 0 0.75rem', color: '#f97316' }}>⚠ Parcelamento já existente</h3>
            <p style={{ color: 'var(--text-muted)', fontSize: '0.875rem', marginBottom: '1rem' }}>
              Encontramos transações parceladas já cadastradas com o mesmo nome e a mesma quantidade
              de parcelas — pode ser uma reimportação da mesma fatura. Escolha como prosseguir:
            </p>
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10, marginBottom: '1.25rem' }}>
              {duplicates.map(d => (
                <div key={`${d.description}-${d.total}`} style={{ background: 'var(--tint-amber-bg)', borderRadius: 8, padding: '0.75rem 1rem' }}>
                  <strong>{d.description}</strong> — {d.total} parcelas
                  <div style={{ fontSize: '0.8rem', color: 'var(--text-muted)', marginTop: 4 }}>
                    {d.existing.length} já cadastrada{d.existing.length > 1 ? 's' : ''}:{' '}
                    {d.existing.map(ex => ex.installment || '?').join(', ')}
                  </div>
                </div>
              ))}
            </div>
            <div style={{ display: 'flex', gap: 8, justifyContent: 'flex-end' }}>
              <button onClick={() => setDuplicates(null)} disabled={loading} style={s.btn('var(--bg-subtle)', 'var(--text-muted)')}>
                Manter e ajustar dados
              </button>
              <button onClick={removeDuplicatesAndRetry} disabled={loading} style={s.btn('#ef4444')}>
                {loading ? 'Removendo...' : '🗑️ Remover existentes e importar'}
              </button>
            </div>
          </div>
        </div>
      )}
      <div style={s.card}>
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'flex-start', marginBottom: '1rem', flexWrap: 'wrap', gap: 8 }}>
          <div>
            <h3 style={{ ...s.h2, marginBottom: '0.4rem' }}>
              🔍 Revisão — {rows.length} lançamentos
            </h3>
            <div style={{ display: 'flex', gap: 8, flexWrap: 'wrap' }}>
              <span style={s.badge('#22c55e')}>{rows.length - unmatched} categorizados</span>
              {unmatched > 0 && <span style={s.badge('#f97316')}>{unmatched} sem categoria</span>}
              {showInstallment && installmentCount > 0 && <span style={s.badge('#6366f1')}>🔁 {installmentCount} parcelado{installmentCount > 1 ? 's' : ''}</span>}
              {rows.filter(r => r.type === 'refund').length > 0 && (
                <span style={s.badge('#22c55e')}>↩ {rows.filter(r => r.type === 'refund').length} reembolso{rows.filter(r => r.type === 'refund').length > 1 ? 's' : ''}</span>
              )}
              {showIncome && incomeCount > 0 && (
                <span style={s.badge('#22c55e')}>💰 {incomeCount} receita{incomeCount > 1 ? 's' : ''}</span>
              )}
            </div>
          </div>
          <div style={{ display: 'flex', gap: 8 }}>
            <button onClick={onReset} style={s.btn('var(--bg-subtle)', 'var(--text-muted)')}>← Novo arquivo</button>
            <button onClick={() => confirm()} disabled={loading} style={s.btn()}>
              {loading ? 'Salvando...' : `✓ Confirmar (${rows.length})`}
            </button>
          </div>
        </div>

        <div style={{ overflowX: 'auto' }}>
          <table style={{ width: '100%', borderCollapse: 'collapse', fontSize: '0.85rem' }}>
            <thead>
              <tr style={{ background: 'var(--bg-subtle)', borderBottom: '2px solid var(--border)' }}>
                {[
                  'Data', 'Descrição',
                  ...(showInstallment ? ['Parcela'] : []),
                  ...(showMethod ? ['Método'] : []),
                  'Tipo', 'Valor (R$)', 'Categoria', 'Subcategoria', 'Detalhes', 'Fixa', '',
                ].map(h => (
                  <th key={h} style={{ padding: '10px 12px', textAlign: 'left', color: 'var(--text-muted)', fontWeight: 600, whiteSpace: 'nowrap' }}>{h}</th>
                ))}
              </tr>
            </thead>
            <tbody>
              {rows.map((row, realIdx) => {
                return (
                  <tr key={realIdx} style={{
                    borderBottom: '1px solid var(--border-light)',
                    background: (row.type === 'refund' || row.type === 'income')
                      ? 'var(--tint-green-bg)'
                      : (!row.category_id ? 'var(--tint-amber-bg)' : 'transparent'),
                  }}>
                    {/* Data */}
                    <td style={{ padding: '8px 12px' }}>
                      <input type="date" value={row.date}
                        onChange={e => setRowField(realIdx, 'date', e.target.value)}
                        style={{ ...s.input, width: 140 }}
                      />
                    </td>
                    {/* Descrição */}
                    <td style={{ padding: '8px 12px', minWidth: 200 }}>
                      <input value={row.description} onChange={e => setRowField(realIdx, 'description', e.target.value)}
                        style={{ ...s.input, width: '100%' }} />
                    </td>
                    {/* Parcela */}
                    {showInstallment && (
                      <td style={{ padding: '8px 12px' }}>
                        <input value={row.installment || ''} placeholder="—"
                          onChange={e => setRowField(realIdx, 'installment', e.target.value || null)}
                          style={{ ...s.input, width: 110 }} />
                      </td>
                    )}
                    {/* Método */}
                    {showMethod && (
                      <td style={{ padding: '8px 12px' }}>
                        <select
                          value={row.method || 'pix'}
                          onChange={e => setRowField(realIdx, 'method', e.target.value)}
                          style={{ ...s.select, width: 100 }}
                        >
                          <option value="pix">Pix</option>
                          <option value="debito">Débito</option>
                        </select>
                      </td>
                    )}
                    {/* Tipo */}
                    <td style={{ padding: '8px 12px' }}>
                      <select
                        value={row.type}
                        onChange={e => setRowField(realIdx, 'type', e.target.value)}
                        style={{
                          ...s.select, width: 120,
                          color: row.type === 'income' ? '#22c55e' : row.type === 'refund' ? '#22c55e' : '#ef4444',
                          fontWeight: 700,
                          borderColor: row.type === 'refund' || row.type === 'income' ? '#22c55e' : 'var(--border)',
                        }}
                      >
                        <option value="expense">Despesa</option>
                        <option value="refund">Reembolso</option>
                        {showIncome && <option value="income">Receita</option>}
                      </select>
                    </td>
                    {/* Valor */}
                    <td style={{ padding: '8px 12px' }}>
                      <input type="number" step="0.01" min="0" value={row.amount}
                        onChange={e => setRowField(realIdx, 'amount', parseFloat(e.target.value) || 0)}
                        style={{ ...s.input, width: 110, color: row.type === 'expense' ? '#ef4444' : '#22c55e', fontWeight: 700 }} />
                    </td>
                    {/* Categoria — despesas/reembolsos usam categorias de despesa; receita usa categorias de receita */}
                    <td style={{ padding: '8px 12px', minWidth: 170 }}>
                      <select
                        value={row.category_id ?? ''}
                        onChange={e => setCategoryForRow(realIdx, e.target.value)}
                        style={{ ...s.select, width: '100%', borderColor: !row.category_id ? '#f97316' : 'var(--border)', background: !row.category_id ? 'var(--tint-amber-bg)' : 'var(--input-bg)' }}
                      >
                        <option value="">⚠ Sem categoria</option>
                        {categoriesForType(row.type).map(c => (
                          <option key={c.id} value={c.id}>{c.name}</option>
                        ))}
                      </select>
                    </td>
                    {/* Subcategoria */}
                    <td style={{ padding: '8px 12px', minWidth: 170 }}>
                      <select
                        value={row.subcategory_id ?? ''}
                        onChange={e => setSubcategoryForRow(realIdx, e.target.value)}
                        style={{ ...s.select, width: '100%' }}
                      >
                        <option value="">—</option>
                        {categoriesForType(row.type).filter(c => c.id !== row.category_id).map(c => (
                          <option key={c.id} value={c.id}>{c.name}</option>
                        ))}
                      </select>
                    </td>
                    {/* Detalhes */}
                    <td style={{ padding: '8px 12px', minWidth: 180 }}>
                      <textarea rows={1} placeholder="Ex: remédio e chocolate" value={row.details || ''}
                        onChange={e => setRowField(realIdx, 'details', e.target.value || null)}
                        style={{ ...s.input, width: '100%', fontFamily: 'inherit', resize: 'vertical', minHeight: 32 }} />
                    </td>
                    {/* Fixa */}
                    <td style={{ padding: '8px 12px', textAlign: 'center' }}>
                      <input type="checkbox" checked={!!row.fixed}
                        onChange={e => setRowField(realIdx, 'fixed', e.target.checked)}
                        style={{ width: 16, height: 16, cursor: 'pointer' }} />
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
  const [importType, setImportType] = useState('fatura');

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
          </div>

          {!preview && (
            <CsvUploader importType={importType} onImportTypeChange={setImportType} onPreview={setPreview} />
          )}

          {preview && (
            <ImportReview preview={preview} categories={categories} importType={importType} onReset={() => setPreview(null)} />
          )}
        </div>
      </div>
    </div>
  );
}