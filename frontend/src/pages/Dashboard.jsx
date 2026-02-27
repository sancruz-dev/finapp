import React, { useState } from 'react';
import dayjs from 'dayjs';
import { PieChart, Pie, Cell, Tooltip, BarChart, Bar, XAxis, YAxis, CartesianGrid, ResponsiveContainer, Legend } from 'recharts';
import { useAuth } from '../context/AuthContext';
import { useTransactions } from '../hooks/useTransactions';
import TransactionModal from '../components/TransactionModal';

const fmt = (v) => new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

export default function Dashboard() {
  const { user, logout } = useAuth();
  const [date, setDate] = useState(dayjs());
  const [showModal, setShowModal] = useState(false);
  const [editing, setEditing] = useState(null);

  const { transactions, summary, loading, add, update, remove } = useTransactions(
    date.month() + 1, date.year()
  );

  const handleSave = async (data) => {
    if (editing) { await update(editing.id, data); setEditing(null); }
    else await add(data);
    setShowModal(false);
  };

  const handleEdit = (tx) => { setEditing(tx); setShowModal(true); };
  const handleDelete = async (id) => { if (window.confirm('Remover?')) await remove(id); };

  return (
    <div style={{ minHeight: '100vh', background: '#f8fafc', fontFamily: 'system-ui, sans-serif' }}>
      {/* Header */}
      <header style={{ background: '#6366f1', color: '#fff', padding: '1rem 2rem', display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
        <h1 style={{ margin: 0, fontSize: '1.25rem' }}>💰 FinApp</h1>
        <div style={{ display: 'flex', gap: 12, alignItems: 'center' }}>
          <span>{user?.name}</span>
          <button onClick={logout} style={{ background: 'rgba(255,255,255,0.2)', border: 'none', color: '#fff', padding: '6px 12px', borderRadius: 6, cursor: 'pointer' }}>Sair</button>
        </div>
      </header>

      <main style={{ maxWidth: 1200, margin: '0 auto', padding: '2rem' }}>
        {/* Navegação de mês */}
        <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center', marginBottom: '2rem' }}>
          <div style={{ display: 'flex', alignItems: 'center', gap: 12 }}>
            <button onClick={() => setDate(d => d.subtract(1, 'month'))} style={{ background: '#fff', border: '1px solid #e2e8f0', padding: '6px 12px', borderRadius: 6, cursor: 'pointer' }}>‹</button>
            <strong style={{ fontSize: '1.1rem', textTransform: 'capitalize' }}>{date.format('MMMM YYYY')}</strong>
            <button onClick={() => setDate(d => d.add(1, 'month'))} style={{ background: '#fff', border: '1px solid #e2e8f0', padding: '6px 12px', borderRadius: 6, cursor: 'pointer' }}>›</button>
          </div>
          <button onClick={() => { setEditing(null); setShowModal(true); }}
            style={{ background: '#6366f1', color: '#fff', border: 'none', padding: '10px 20px', borderRadius: 8, cursor: 'pointer', fontWeight: 600 }}>
            + Nova transação
          </button>
        </div>

        {/* Cards de resumo */}
        <div style={{ display: 'grid', gridTemplateColumns: 'repeat(3, 1fr)', gap: '1rem', marginBottom: '2rem' }}>
          {[
            { label: 'Receitas', value: summary?.total_income, color: '#22c55e' },
            { label: 'Despesas', value: summary?.total_expense, color: '#ef4444' },
            { label: 'Saldo', value: summary?.balance, color: '#6366f1' },
          ].map(({ label, value, color }) => (
            <div key={label} style={{ background: '#fff', padding: '1.5rem', borderRadius: 12, boxShadow: '0 1px 4px #0001', borderLeft: `4px solid ${color}` }}>
              <p style={{ margin: 0, color: '#64748b', fontSize: '0.875rem' }}>{label}</p>
              <p style={{ margin: '4px 0 0', fontSize: '1.5rem', fontWeight: 700, color }}>{fmt(value)}</p>
            </div>
          ))}
        </div>

        <div style={{ display: 'grid', gridTemplateColumns: '1fr 1fr', gap: '1.5rem', marginBottom: '2rem' }}>
          {/* Gráfico pizza - gastos por categoria */}
          <div style={{ background: '#fff', padding: '1.5rem', borderRadius: 12, boxShadow: '0 1px 4px #0001' }}>
            <h3 style={{ margin: '0 0 1rem', fontSize: '1rem' }}>Gastos por Categoria</h3>
            {summary?.by_category?.length > 0 ? (
              <ResponsiveContainer width="100%" height={200}>
                <PieChart>
                  <Pie data={summary.by_category} dataKey="total" nameKey="name" cx="50%" cy="50%" outerRadius={80}>
                    {summary.by_category.map((c, i) => <Cell key={i} fill={c.color} />)}
                  </Pie>
                  <Tooltip formatter={(v) => fmt(v)} />
                  <Legend />
                </PieChart>
              </ResponsiveContainer>
            ) : <p style={{ color: '#94a3b8', textAlign: 'center', paddingTop: 60 }}>Sem dados</p>}
          </div>

          {/* Gráfico barras receita vs despesa por dia */}
          <div style={{ background: '#fff', padding: '1.5rem', borderRadius: 12, boxShadow: '0 1px 4px #0001' }}>
            <h3 style={{ margin: '0 0 1rem', fontSize: '1rem' }}>Receitas vs Despesas</h3>
            {summary ? (
              <ResponsiveContainer width="100%" height={200}>
                <BarChart data={[{ name: date.format('MMM'), Receitas: summary.total_income, Despesas: summary.total_expense }]}>
                  <CartesianGrid strokeDasharray="3 3" />
                  <XAxis dataKey="name" />
                  <YAxis tickFormatter={(v) => `R$${v}`} />
                  <Tooltip formatter={(v) => fmt(v)} />
                  <Legend />
                  <Bar dataKey="Receitas" fill="#22c55e" radius={[4,4,0,0]} />
                  <Bar dataKey="Despesas" fill="#ef4444" radius={[4,4,0,0]} />
                </BarChart>
              </ResponsiveContainer>
            ) : null}
          </div>
        </div>

        {/* Lista de transações */}
        <div style={{ background: '#fff', borderRadius: 12, boxShadow: '0 1px 4px #0001', overflow: 'hidden' }}>
          <div style={{ padding: '1rem 1.5rem', borderBottom: '1px solid #f1f5f9' }}>
            <h3 style={{ margin: 0 }}>Transações</h3>
          </div>
          {loading ? (
            <p style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Carregando...</p>
          ) : transactions.length === 0 ? (
            <p style={{ textAlign: 'center', padding: '2rem', color: '#94a3b8' }}>Nenhuma transação neste mês</p>
          ) : transactions.map(tx => (
            <div key={tx.id} style={{ display: 'flex', alignItems: 'center', padding: '0.875rem 1.5rem', borderBottom: '1px solid #f8fafc', gap: 12 }}>
              <div style={{ width: 10, height: 10, borderRadius: '50%', background: tx.category_color || '#94a3b8', flexShrink: 0 }} />
              <div style={{ flex: 1 }}>
                <p style={{ margin: 0, fontWeight: 500 }}>{tx.description}</p>
                <p style={{ margin: 0, fontSize: '0.75rem', color: '#94a3b8' }}>
                  {tx.category_name} · {dayjs(tx.date).format('DD/MM/YYYY')}
                </p>
              </div>
              <span style={{ fontWeight: 700, color: tx.type === 'income' ? '#22c55e' : '#ef4444' }}>
                {tx.type === 'income' ? '+' : '-'}{fmt(tx.amount)}
              </span>
              <button onClick={() => handleEdit(tx)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#6366f1' }}>✏️</button>
              <button onClick={() => handleDelete(tx.id)} style={{ background: 'none', border: 'none', cursor: 'pointer', color: '#ef4444' }}>🗑️</button>
            </div>
          ))}
        </div>
      </main>

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
