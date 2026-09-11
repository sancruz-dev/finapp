import React, { useState } from 'react';
import dayjs from 'dayjs';
import Header, { HEADER_HEIGHT } from '../components/Header';
import InvestmentModal from '../components/InvestmentModal';
import { useInvestments } from '../hooks/useInvestments';

const fmt = (v) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

const ASSET_TYPE_LABELS = { CDB: 'CDB', LCI: 'LCI', LCA: 'LCA', TESOURO: 'Tesouro Direto', POUPANCA: 'Poupança' };

const indexerLabel = (inv) => {
  if (inv.indexer === 'POUPANCA') return 'Poupança';
  if (inv.indexer === 'PREFIXADO') return `${inv.indexer_rate}% a.a. (prefixado)`;
  return `${inv.indexer_rate}% do ${inv.indexer === 'SELIC' ? 'Selic' : 'CDI'}`;
};

const s = {
  page: { flex: 1, background: 'var(--bg-page)', fontFamily: "'Segoe UI', system-ui, sans-serif", padding: '2rem 1.5rem', paddingTop: `calc(2rem + ${HEADER_HEIGHT}px)` },
  card: { background: 'var(--bg-card)', borderRadius: 14, boxShadow: '0 1px 6px rgba(0,0,0,0.07)' },
  btn: (bg = '#6366f1', color = '#fff') => ({
    background: bg, color, border: 'none', borderRadius: 8,
    padding: '8px 16px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem',
  }),
};

export default function InvestmentsPage() {
  const { investments, summary, loading, add, update, remove } = useInvestments();
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);

  const handleSave = async (data) => {
    if (editing) { await update(editing.id, data); setEditing(null); }
    else await add(data);
    setShowModal(false);
  };
  const handleEdit = (inv) => { setEditing(inv); setShowModal(true); };
  const handleDelete = async (id) => { if (window.confirm('Remover este investimento?')) await remove(id); };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', flexDirection: 'column' }}>
      <Header showBack />
      <div style={s.page}>
        <div style={{ maxWidth: 1100, margin: '0 auto' }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.5rem' }}>
            <h1 style={{ margin: 0, fontSize: '1.4rem', color: 'var(--text-primary)' }}>💹 Investimentos</h1>
            <button onClick={() => { setEditing(null); setShowModal(true); }} style={s.btn()}>
              + Novo Investimento
            </button>
          </div>

          {/* Cards de resumo */}
          <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1.75rem' }}>
            {[
              { label: 'Total Aplicado', value: summary?.total_principal, color: 'var(--text-primary)' },
              { label: 'Valor Bruto Atual', value: summary?.total_gross, color: '#22c55e' },
              { label: 'Valor Líquido Atual', value: summary?.total_net, color: '#6366f1' },
            ].map(({ label, value, color }) => (
              <div key={label} style={{ ...s.card, padding: '1.25rem 1.5rem' }}>
                <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '0.8rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</p>
                <p style={{ margin: '6px 0 0', fontSize: '1.6rem', fontWeight: 800, color, lineHeight: 1 }}>{fmt(value)}</p>
              </div>
            ))}
          </div>

          {/* Lista de ativos */}
          <div style={{ ...s.card, padding: '1.5rem' }}>
            {loading ? (
              <p style={{ color: 'var(--text-muted)' }}>Carregando...</p>
            ) : investments.length === 0 ? (
              <p style={{ color: 'var(--text-muted)' }}>Nenhum investimento cadastrado ainda.</p>
            ) : (
              <div style={{ overflowX: 'auto' }}>
                <table style={{ width: '100%', borderCollapse: 'collapse' }}>
                  <thead>
                    <tr style={{ borderBottom: '2px solid var(--border-light)' }}>
                      {['Instituição', 'Tipo', 'Indexador', 'Aplicado em', 'Valor aplicado', 'Valor bruto', 'Valor líquido', 'Vencimento', ''].map(h => (
                        <th key={h} style={{ textAlign: 'left', padding: '8px 12px', fontSize: '0.75rem', fontWeight: 700, color: 'var(--text-muted)', textTransform: 'uppercase', letterSpacing: '0.03em' }}>{h}</th>
                      ))}
                    </tr>
                  </thead>
                  <tbody>
                    {investments.map(inv => (
                      <tr key={inv.id} style={{ borderBottom: '1px solid var(--border-light)' }}>
                        <td style={{ padding: '10px 12px', fontWeight: 600, color: 'var(--text-primary)' }}>{inv.institution}</td>
                        <td style={{ padding: '10px 12px', color: 'var(--text-secondary)' }}>{ASSET_TYPE_LABELS[inv.asset_type] || inv.asset_type}</td>
                        <td style={{ padding: '10px 12px', color: 'var(--text-secondary)' }}>{indexerLabel(inv)}</td>
                        <td style={{ padding: '10px 12px', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{dayjs(inv.applied_at).format('DD/MM/YYYY')}</td>
                        <td style={{ padding: '10px 12px', color: 'var(--text-secondary)', whiteSpace: 'nowrap' }}>{fmt(inv.principal_amount)}</td>
                        <td style={{ padding: '10px 12px', color: '#22c55e', fontWeight: 600, whiteSpace: 'nowrap' }}>{fmt(inv.gross_value)}</td>
                        <td style={{ padding: '10px 12px', color: '#6366f1', fontWeight: 600, whiteSpace: 'nowrap' }}>{fmt(inv.net_value)}</td>
                        <td style={{ padding: '10px 12px', color: 'var(--text-faint)', whiteSpace: 'nowrap' }}>{inv.maturity_at ? dayjs(inv.maturity_at).format('DD/MM/YYYY') : '—'}</td>
                        <td style={{ padding: '10px 12px', whiteSpace: 'nowrap' }}>
                          <button onClick={() => handleEdit(inv)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '0.95rem', marginRight: 8 }} title="Editar">✏️</button>
                          <button onClick={() => handleDelete(inv.id)} style={{ background: 'none', border: 'none', cursor: 'pointer', fontSize: '0.95rem' }} title="Remover">🗑️</button>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            )}
          </div>
        </div>
      </div>

      {showModal && (
        <InvestmentModal
          initial={editing}
          onSave={handleSave}
          onClose={() => { setShowModal(false); setEditing(null); }}
        />
      )}
    </div>
  );
}
