import React, { useState, useRef, useEffect } from 'react';
import { useNavigate, useLocation } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';

export const HEADER_HEIGHT = 60;

const MENU_ITEMS = [
  { path: '/import', icon: '📥', label: 'Importar CSV' },
  { path: '/investimentos', icon: '💹', label: 'Investimentos' },
  { path: '/merchants', icon: '🏪', label: 'Pessoas & Comércios' },
  { path: '/settings', icon: '⚙️', label: 'Configurações' },
];

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
  right: { display: 'flex', gap: 12, alignItems: 'center', position: 'relative' },
  user: { fontSize: '0.9rem', opacity: 0.85 },
  menuBtn: {
    background: 'rgba(255,255,255,0.14)',
    border: '1px solid rgba(255,255,255,0.22)',
    borderRadius: 8,
    width: 36,
    height: 36,
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    cursor: 'pointer',
    fontSize: '1.1rem',
    lineHeight: 1,
    color: '#fff',
    padding: 0,
  },
  dropdown: {
    position: 'absolute',
    top: HEADER_HEIGHT - 8,
    right: 0,
    minWidth: 220,
    background: 'var(--bg-card)',
    borderRadius: 10,
    boxShadow: '0 8px 24px rgba(0,0,0,0.25)',
    border: '1px solid var(--border)',
    padding: 6,
    display: 'flex',
    flexDirection: 'column',
    gap: 2,
    zIndex: 200,
  },
  item: (active) => ({
    display: 'flex',
    alignItems: 'center',
    gap: 10,
    padding: '9px 12px',
    borderRadius: 7,
    border: 'none',
    background: active ? 'var(--bg-subtle)' : 'transparent',
    color: 'var(--text-primary)',
    fontSize: '0.85rem',
    fontWeight: active ? 700 : 500,
    cursor: 'pointer',
    textAlign: 'left',
    width: '100%',
  }),
  divider: { height: 1, background: 'var(--border-light)', margin: '4px 0' },
  logoutItem: {
    display: 'flex',
    alignItems: 'center',
    gap: 10,
    padding: '9px 12px',
    borderRadius: 7,
    border: 'none',
    background: 'transparent',
    color: '#ef4444',
    fontSize: '0.85rem',
    fontWeight: 600,
    cursor: 'pointer',
    textAlign: 'left',
    width: '100%',
  },
};

const menuHoverStyle = `
  .header-menu-item:hover { background: var(--bg-subtle) !important; }
  .header-menu-logout:hover { background: rgba(239, 68, 68, 0.39) !important; }
  .header-menu-btn:hover { background: rgba(255, 255, 255, 0.42) !important; }
`;

export default function Header({ showBack = false, children }) {
  const { user, logout } = useAuth();
  const { theme, toggleTheme } = useTheme();
  const navigate = useNavigate();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const menuRef = useRef(null);

  useEffect(() => {
    if (!menuOpen) return;
    const onClickOutside = (e) => {
      if (menuRef.current && !menuRef.current.contains(e.target)) setMenuOpen(false);
    };
    document.addEventListener('mousedown', onClickOutside);
    return () => document.removeEventListener('mousedown', onClickOutside);
  }, [menuOpen]);

  const go = (path) => {
    setMenuOpen(false);
    navigate(path);
  };

  return (
    <header style={s.header}>
      <style>{menuHoverStyle}</style>
      <div style={s.left}>
        {showBack && (
          <button onClick={() => navigate('/')} style={s.backBtn} title="Voltar ao início">
            ‹ Voltar
          </button>
        )}
        <span style={s.brand} onClick={() => navigate('/')}>💰 FinApp</span>
      </div>
      <div style={s.right} ref={menuRef}>
        {children}
        <span style={s.user}>{user?.name}</span>
        <button
          className="header-menu-btn"
          onClick={() => setMenuOpen(o => !o)}
          style={s.menuBtn}
          title="Menu"
        >
          ☰
        </button>

        {menuOpen && (
          <div style={s.dropdown}>
            {MENU_ITEMS.map(item => (
              <button
                key={item.path}
                className="header-menu-item"
                onClick={() => go(item.path)}
                style={s.item(location.pathname === item.path)}
              >
                <span>{item.icon}</span>{item.label}
              </button>
            ))}
            <div style={s.divider} />
            <button className="header-menu-item" onClick={() => { toggleTheme(); setMenuOpen(false); }} style={s.item(false)}>
              <span>{theme === 'dark' ? '☀️' : '🌙'}</span>
              {theme === 'dark' ? 'Tema claro' : 'Tema escuro'}
            </button>
            <div style={s.divider} />
            <button className="header-menu-logout" onClick={logout} style={s.logoutItem}>
              <span>🚪</span>Sair
            </button>
          </div>
        )}
      </div>
    </header>
  );
}
