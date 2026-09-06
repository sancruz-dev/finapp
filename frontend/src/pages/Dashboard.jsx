import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import dayjs from 'dayjs';
import 'dayjs/locale/pt-br';
import {
  PieChart, Pie, Cell, Tooltip,
  BarChart, Bar, XAxis, YAxis, CartesianGrid,
  ResponsiveContainer, Legend
} from 'recharts';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { useTransactions } from '../hooks/useTransactions';
import TransactionModal from '../components/TransactionModal';
import AiSidebar from '../components/AiSidebar';
import { HEADER_HEIGHT } from '../components/Header';

dayjs.locale('pt-br');

const fmt = (v) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

const CustomLegend = ({ payload }) => (
  <div style={{ display: 'flex', flexWrap: 'wrap', gap: '8px', justifyContent: 'center', marginTop: 8 }}>
    {payload.map((entry, i) => (
      <span key={i} style={{ display: 'flex', alignItems: 'center', gap: 4, fontSize: '0.78rem', color: '#475569' }}>
        <span style={{ width: 10, height: 10, borderRadius: '50%', background: entry.color, display: 'inline-block' }} />
        {entry.value}
      </span>
    ))}
  </div>
);

const TYPE_CONFIG = {
  income:  { label: 'Receita',   color: '#22c55e', sign: '+' },
  expense: { label: 'Despesa',   color: '#ef4444', sign: '-' },
  refund:  { label: 'Reembolso', color: '#6366f1', sign: '↩' },
};

export default function Dashboard() {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const [date, setDate] = useState(dayjs());
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);
  const [aiOpen, setAiOpen] = useState(true);
  const navigate = useNavigate();

  const [filterType, setFilterType] = useState('');
  const [filterCategory, setFilterCategory] = useState('');

  const { transactions, summary, loading, add, update, remove } = useTransactions(
    date.month() + 1, date.year()
  );

  const handleSave = async (data) => {
    if (editing) { await update(editing.id, data); setEditing(null); }
    else await add(data);
    setShowModal(false);
  };
  const handleEdit = (tx) => { setEditing(tx); setShowModal(true); };
  const handleDelete = async (id) => { if (window.confirm('Remover esta transação?')) await remove(id); };

  const filtered = transactions.filter(tx => {
    if (filterType && tx.type !== filterType) return false;
    if (filterCategory && String(tx.category_id) !== filterCategory) return false;
    return true;
  });

  const uniqueCategories = Array.from(
    new Map(
      transactions
        .filter(tx => tx.category_id)
        .map(tx => [tx.category_id, { id: tx.category_id, name: tx.category_name, color: tx.category_color }])
    ).values()
  );

  const s = {
    page: {
      minHeight: '100vh',
      paddingTop: HEADER_HEIGHT,
      background: 'var(--bg-page)',
      fontFamily: "'Segoe UI', system-ui, sans-serif",
      display: 'flex',
      flexDirection: 'column',
    },
    layout: {
      display: 'flex',
      flex: 1,
      minHeight: 0,
    },
    main: {
      flex: 1,
      minWidth: 0,
      overflowY: 'auto',
    },
    header: {
      position: 'fixed',
      top: 0,
      left: 0,
      right: 0,
      zIndex: 100,
      background: 'linear-gradient(135deg, #6366f1 0%, #4f46e5 100%)',
      color: '#fff', padding: '0 2rem', height: HEADER_HEIGHT,
      display: 'flex', justifyContent: 'space-between', alignItems: 'center',
      boxShadow: '0 2px 8px rgba(99,102,241,0.35)',
      flexShrink: 0,
    },
    content: { maxWidth: 1100, margin: '0 auto', padding: '2rem 1.5rem' },
    card: { background: 'var(--bg-card)', borderRadius: 14, boxShadow: '0 1px 6px rgba(0,0,0,0.07)' },
    btn: (bg = '#6366f1', color = '#fff') => ({
      background: bg, color, border: 'none', borderRadius: 8,
      padding: '8px 16px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem',
    }),
    select: {
      padding: '7px 10px', borderRadius: 7, border: '1px solid var(--border)',
      fontSize: '0.85rem', background: 'var(--input-bg)', color: 'var(--text-secondary)', cursor: 'pointer',
    },
    aiToggle: {
      background: aiOpen ? 'rgba(255,255,255,0.22)' : 'rgba(255,255,255,0.12)',
      color: '#fff',
      border: `1px solid ${aiOpen ? 'rgba(255,255,255,0.4)' : 'rgba(255,255,255,0.2)'}`,
      borderRadius: 8,
      padding: '6px 14px',
      cursor: 'pointer',
      fontWeight: 600,
      fontSize: '0.8rem',
      display: 'flex',
      alignItems: 'center',
      gap: 6,
      transition: 'all 0.15s',
    },
    themeToggle: {
      background: 'rgba(255,255,255,0.14)',
      border: '1px solid rgba(255,255,255,0.22)',
      borderRadius: 8,
      width: 32,
      height: 32,
      display: 'flex',
      alignItems: 'center',
      justifyContent: 'center',
      cursor: 'pointer',
      fontSize: '1rem',
      lineHeight: 1,
      color: '#fff',
      padding: 0,
    },
  };

  return (
    <div style={s.page}>

      {/* ── Header fixo no topo ── */}
      <header style={s.header}>
        <span style={{ fontWeight: 700, fontSize: '1.1rem', letterSpacing: '-0.3px' }}>💰 FinApp</span>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <span style={{ fontSize: '0.9rem', opacity: 0.85 }}>{user?.name}</span>
          <button
            onClick={toggleTheme}
            style={s.themeToggle}
            title={theme === 'dark' ? 'Mudar para tema claro' : 'Mudar para tema escuro'}
          >
            {theme === 'dark' ? '☀️' : '🌙'}
          </button>
          <button
            onClick={() => setAiOpen(o => !o)}
            style={s.aiToggle}
            title={aiOpen ? 'Fechar assistente' : 'Abrir assistente IA'}
          >
            ✦ {aiOpen ? 'Fechar IA' : 'Assistente IA'}
          </button>
          <button onClick={logout} style={{ ...s.btn('rgba(255,255,255,0.18)', '#fff'), fontSize: '0.8rem', padding: '6px 14px' }}>
            Sair
          </button>
        </div>
      </header>

      {/* ── Layout: conteúdo principal + sidebar IA ── */}
      <div style={s.layout}>

        {/* ── Main content ── */}
        <div style={s.main}>
          <div style={s.content}>

            {/* Navegação de mês */}
            <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '1.75rem' }}>
              <div style={{ display: 'flex', alignItems: 'center', gap: 10 }}>
                <button onClick={() => setDate(d => d.subtract(1, 'month'))} style={s.btn('var(--bg-card)', 'var(--text-secondary)')}>‹</button>
                <strong style={{ fontSize: '1.05rem', textTransform: 'capitalize', minWidth: 130, textAlign: 'center', color: 'var(--text-primary)' }}>
                  {date.format('MMMM YYYY')}
                </strong>
                <button onClick={() => setDate(d => d.add(1, 'month'))} style={s.btn('var(--bg-card)', 'var(--text-secondary)')}>›</button>
              </div>
              <div style={{ display: 'flex', gap: 8 }}>
                <button onClick={() => { setEditing(null); setShowModal(true); }} style={s.btn()}>
                  + Nova transação
                </button>
                <button onClick={() => navigate('/import')} style={s.btn('#818cf8')}>
                  📥 Importar CSV
                </button>
                <button onClick={() => navigate('/merchants')} style={s.btn('#818cf8')}>
                  🏪 Comerciantes
                </button>
              </div>
            </div>

            {/* Cards de resumo */}
            <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '1.75rem' }}>
              {[
                { label: 'Receitas', value: summary?.total_income,  color: '#22c55e', bg: '#f0fdf4', border: '#bbf7d0' },
                { label: 'Despesas', value: summary?.total_expense, color: '#ef4444', bg: '#fef2f2', border: '#fecaca' },
                { label: 'Saldo',    value: summary?.balance,       color: '#6366f1', bg: '#eef2ff', border: '#c7d2fe' },
              ].map(({ label, value, color, bg, border }) => (
                <div key={label} style={{ ...s.card, padding: '1.25rem 1.5rem', background: bg, border: `1px solid ${border}` }}>
                  <p style={{ margin: 0, color: 'var(--text-muted)', fontSize: '0.8rem', fontWeight: 600, textTransform: 'uppercase', letterSpacing: '0.05em' }}>{label}</p>
                  <p style={{ margin: '6px 0 0', fontSize: '1.6rem', fontWeight: 800, color, lineHeight: 1 }}>{fmt(value)}</p>
                </div>
              ))}
            </div>

            {/* Gráficos */}
            <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.25rem', marginBottom: '1.75rem' }}>
              <div style={{ ...s.card, padding: '1.5rem' }}>
                <h3 style={{ margin: '0 0 1rem', fontSize: '0.95rem', color: 'var(--text-primary)' }}>Gastos por Categoria</h3>
                {summary?.by_category?.length > 0 ? (
                  <div style={{ width: '100%', height: 220 }}>
                    <ResponsiveContainer width="100%" height="100%">
                      <PieChart>
                        <Pie data={summary.by_category} dataKey="total" nameKey="name" cx="50%" cy="45%" outerRadius={80} strokeWidth={2}>
                          {summary.by_category.map((c, i) => <Cell key={i} fill={c.color} stroke={c.color} />)}
                        </Pie>
                        <Tooltip formatter={(v) => fmt(v)} />
                        <Legend content={<CustomLegend />} />
                      </PieChart>
                    </ResponsiveContainer>
                  </div>
                ) : (
                  <div style={{ height: 220, display: 'flex', alignItems: 'center', justifyContent: 'center', color: 'var(--text-faint)', fontSize: '0.875rem' }}>
                    Nenhuma despesa neste mês
                  </div>
                )}
              </div>

              <div style={{ ...s.card, padding: '1.5rem' }}>
                <h3 style={{ margin: '0 0 1rem', fontSize: '0.95rem', color: 'var(--text-primary)' }}>Receitas vs Despesas</h3>
                <div style={{ width: '100%', height: 220 }}>
                  <ResponsiveContainer width="100%" height="100%">
                    <BarChart
                      data={[{
                        name: date.format('MMM'),
                        Receitas: parseFloat(summary?.total_income  || 0),
                        Despesas: parseFloat(summary?.total_expense || 0),
                      }]}
                      margin={{ top: 5, right: 10, left: 0, bottom: 5 }}
                    >
                      <CartesianGrid strokeDasharray="3 3" stroke="#f1f5f9" />
                      <XAxis dataKey="name" tick={{ fontSize: 12 }} />
                      <YAxis tickFormatter={(v) => `R$${v}`} tick={{ fontSize: 11 }} width={60} />
                      <Tooltip formatter={(v) => fmt(v)} />
                      <Legend />
                      <Bar dataKey="Receitas" fill="#22c55e" radius={[6, 6, 0, 0]} maxBarSize={60} />
                      <Bar dataKey="Despesas" fill="#ef4444" radius={[6, 6, 0, 0]} maxBarSize={60} />
                    </BarChart>
                  </ResponsiveContainer>
                </div>
              </div>
            </div>

            {/* Lista de transações */}
            <div style={s.card}>
              <div style={{
                padding: '1rem 1.5rem', borderBottom: '1px solid var(--border-light)',
                display: 'flex', justifyContent: 'space-between', alignItems: 'center',
                flexWrap: 'wrap', gap: 10,
              }}>
                <h3 style={{ margin: 0, fontSize: '0.95rem', color: 'var(--text-primary)' }}>
                  Transações
                  {filtered.length !== transactions.length && (
                    <span style={{ marginLeft: 8, fontSize: '0.78rem', color: 'var(--text-faint)', fontWeight: 400 }}>
                      ({filtered.length} de {transactions.length})
                    </span>
                  )}
                </h3>
                <div style={{ display: 'flex', gap: 8, alignItems: 'center', flexWrap: 'wrap' }}>
                  <select value={filterType} onChange={e => setFilterType(e.target.value)} style={s.select}>
                    <option value="">Todos os tipos</option>
                    <option value="income">Receitas</option>
                    <option value="expense">Despesas</option>
                    <option value="refund">Reembolsos</option>
                  </select>
                  <select value={filterCategory} onChange={e => setFilterCategory(e.target.value)} style={s.select}>
                    <option value="">Todas as categorias</option>
                    {uniqueCategories.map(c => (
                      <option key={c.id} value={String(c.id)}>{c.name}</option>
                    ))}
                  </select>
                  {(filterType || filterCategory) && (
                    <button onClick={() => { setFilterType(''); setFilterCategory(''); }}
                      style={{ ...s.btn('var(--bg-subtle)', 'var(--text-muted)'), fontWeight: 500 }}>
                      ✕ Limpar
                    </button>
                  )}
                </div>
              </div>

              {loading ? (
                <p style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-faint)' }}>Carregando...</p>
              ) : filtered.length === 0 ? (
                <p style={{ textAlign: 'center', padding: '2.5rem', color: 'var(--text-faint)' }}>
                  {transactions.length === 0 ? 'Nenhuma transação neste mês' : 'Nenhuma transação para os filtros selecionados'}
                </p>
              ) : filtered.map((tx, idx) => {
                const typeCfg = TYPE_CONFIG[tx.type] ?? TYPE_CONFIG.expense;
                return (
                  <div
                    key={tx.id}
                    style={{
                      display: 'flex', alignItems: 'center', padding: '0.875rem 1.5rem',
                      borderBottom: idx < filtered.length - 1 ? '1px solid var(--border-light)' : 'none',
                      gap: 12, transition: 'background 0.15s',
                    }}
                    onMouseEnter={e => e.currentTarget.style.background = 'var(--bg-hover)'}
                    onMouseLeave={e => e.currentTarget.style.background = 'transparent'}
                  >
                    <div style={{
                      width: 10, height: 10, borderRadius: '50%',
                      background: tx.category_color || '#cbd5e1', flexShrink: 0,
                    }} />
                    <div style={{ flex: 1, minWidth: 0 }}>
                      <p style={{ margin: 0, fontWeight: 600, fontSize: '0.9rem', color: 'var(--text-primary)', whiteSpace: 'nowrap', overflow: 'hidden', textOverflow: 'ellipsis' }}>
                        {tx.description}
                      </p>
                      <p style={{ margin: 0, fontSize: '0.75rem', color: 'var(--text-faint)', marginTop: 2 }}>
                        {tx.category_name || 'Sem categoria'} · {dayjs(tx.date).format('DD/MM/YYYY')}
                      </p>
                    </div>
                    {tx.type === 'refund' && (
                      <span style={{
                        fontSize: '0.7rem', background: '#eef2ff', color: '#6366f1',
                        borderRadius: 20, padding: '2px 8px', fontWeight: 700, whiteSpace: 'nowrap',
                      }}>↩ Reembolso</span>
                    )}
                    <span style={{ fontWeight: 700, fontSize: '0.95rem', color: typeCfg.color, whiteSpace: 'nowrap' }}>
                      {typeCfg.sign}{fmt(tx.amount)}
                    </span>
                    <button onClick={() => handleEdit(tx)} title="Editar"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6366f1', fontSize: '1rem', padding: '2px 4px', borderRadius: 4 }}>✏️</button>
                    <button onClick={() => handleDelete(tx.id)} title="Remover"
                      style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#ef4444', fontSize: '1rem', padding: '2px 4px', borderRadius: 4 }}>🗑️</button>
                  </div>
                );
              })}
            </div>

          </div>
        </div>

        {/* ── Sidebar IA (togglável) ── */}
        {aiOpen && <AiSidebar topOffset={HEADER_HEIGHT} />}

      </div>

      {showModal && (
        <TransactionModal
          initial={editing}
          onSave={handleSave}
          onClose={() => { setShowModal(false); setEditing(null); }}
        />
      )}
    </div>
  );
}