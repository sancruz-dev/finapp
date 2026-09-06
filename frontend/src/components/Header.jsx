import React from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';

export const HEADER_HEIGHT = 60;

const s = {
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
    fontFamily: "'Segoe UI', system-ui, sans-serif",
  },
  left: { display: 'flex', alignItems: 'center', gap: 14 },
  brand: { fontWeight: 700, fontSize: '1.1rem', letterSpacing: '-0.3px', cursor: 'pointer' },
  backBtn: {
    background: 'rgba(255,255,255,0.12)', color: '#fff', border: 'none',
    borderRadius: 8, padding: '6px 12px', cursor: 'pointer',
    fontWeight: 600, fontSize: '0.8rem', display: 'flex', alignItems: 'center', gap: 4,
  },
  right: { display: 'flex', gap: 12, alignItems: 'center' },
  user: { fontSize: '0.9rem', opacity: 0.85 },
  btn: (bg = 'rgba(255,255,255,0.18)', color = '#fff') => ({
    background: bg, color, border: 'none', borderRadius: 8,
    padding: '6px 14px', cursor: 'pointer', fontWeight: 600, fontSize: '0.8rem',
  }),
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

export default function Header({ showBack = false, children }) {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();

  return (
    <header style={s.header}>
      <div style={s.left}>
        {showBack && (
          <button onClick={() => navigate('/')} style={s.backBtn} title="Voltar ao início">
            ‹ Voltar
          </button>
        )}
        <span style={s.brand} onClick={() => navigate('/')}>💰 FinApp</span>
      </div>
      <div style={s.right}>
        {children}
        <button
          onClick={toggleTheme}
          style={s.themeToggle}
          title={theme === 'dark' ? 'Mudar para tema claro' : 'Mudar para tema escuro'}
        >
          {theme === 'dark' ? '☀️' : '🌙'}
        </button>
        <span style={s.user}>{user?.name}</span>
        <button onClick={logout} style={s.btn()}>Sair</button>
      </div>
    </header>
  );
}
