import React, { useState, useEffect } from 'react';
import { useAuth } from '../context/AuthContext';
import { useTheme } from '../context/ThemeContext';
import { userService, categoryService } from '../services/api';
import Header, { HEADER_HEIGHT } from '../components/Header';

const fmt = (v) =>
  new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' }).format(v || 0);

const s = {
  page: { minHeight: '100vh', paddingTop: HEADER_HEIGHT, background: 'var(--bg-page)', fontFamily: "'Segoe UI', system-ui, sans-serif" },
  content: { maxWidth: 720, margin: '0 auto', padding: '2rem 1.5rem', display: 'flex', flexDirection: 'column', gap: '1.5rem' },
  card: { background: 'var(--bg-card)', borderRadius: 14, boxShadow: '0 1px 6px rgba(0,0,0,0.07)', padding: '1.5rem' },
  title: { margin: '0 0 1.25rem', fontSize: '1rem', color: 'var(--text-primary)' },
  label: { display: 'block', marginBottom: 4, fontWeight: 500, fontSize: '0.875rem', color: 'var(--text-primary)' },
  input: { width: '100%', padding: '8px 12px', borderRadius: 6, border: '1px solid var(--border)', fontSize: '0.95rem', boxSizing: 'border-box', background: 'var(--input-bg)', color: 'var(--text-primary)' },
  field: { marginBottom: '1rem' },
  btn: (bg = '#6366f1', color = '#fff') => ({
    background: bg, color, border: 'none', borderRadius: 8,
    padding: '8px 16px', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem',
  }),
  msg: (ok) => ({ fontSize: '0.82rem', marginTop: 8, color: ok ? '#22c55e' : '#ef4444', fontWeight: 600 }),
  btnSm: { padding: '8px 16px', borderRadius: 8, border: 'none', cursor: 'pointer', fontWeight: 600, fontSize: '0.875rem' },
  badge: (color = '#6366f1') => ({
    display: 'inline-flex', alignItems: 'center', gap: 4,
    background: color + '22', color, borderRadius: 20, padding: '2px 10px', fontSize: '0.75rem', fontWeight: 600,
  }),
  tag: { display: 'inline-flex', alignItems: 'center', gap: 4, background: 'var(--bg-subtle)',
         borderRadius: 20, padding: '3px 10px', fontSize: '0.78rem', color: 'var(--text-secondary)' },
};

// ══════════════════════════════════════════════════════════════════════════
// KeywordsManager — palavras-chave usadas para categorizar lançamentos importados
// ══════════════════════════════════════════════════════════════════════════
function KeywordsManager({ categories, onCategoriesChange }) {
  const [selected, setSelected] = useState(null);
  const [newKw, setNewKw]       = useState('');
  const [loading, setLoading]   = useState(false);

  const cat = categories.find(c => c.id === selected);

  const reload = async () => {
    const res = await categoryService.list();
    onCategoriesChange(res.data);
  };

  const addKeyword = async () => {
    if (!newKw.trim() || !selected) return;
    setLoading(true);
    try {
      await categoryService.addKeyword(selected, newKw.trim());
      await reload();
      setNewKw('');
    } finally { setLoading(false); }
  };

  const removeKeyword = async (kwId) => {
    await categoryService.removeKeyword(kwId);
    await reload();
  };

  return (
    <div style={s.card}>
      <h3 style={s.title}>🏷️ Palavras-chave por categoria</h3>
      <p style={{ margin: '-0.75rem 0 1.25rem', fontSize: '0.8rem', color: 'var(--text-faint)' }}>
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

export default function SettingsPage() {
  const { user, updateUser } = useAuth();
  const { theme, toggleTheme } = useTheme();

  // ── Perfil ──────────────────────────────────────────────────────────────
  const [name, setName] = useState(user?.name || '');
  const [savingName, setSavingName] = useState(false);
  const [nameMsg, setNameMsg] = useState(null);

  const handleSaveName = async () => {
    if (!name.trim()) return;
    setSavingName(true);
    setNameMsg(null);
    try {
      const { data } = await userService.updateProfile(name.trim());
      updateUser({ name: data.name });
      setNameMsg({ ok: true, text: 'Nome atualizado.' });
    } catch (err) {
      setNameMsg({ ok: false, text: err.response?.data?.error || 'Erro ao atualizar nome.' });
    } finally {
      setSavingName(false);
    }
  };

  // ── Senha ───────────────────────────────────────────────────────────────
  const [currentPassword, setCurrentPassword] = useState('');
  const [newPassword, setNewPassword] = useState('');
  const [confirmPassword, setConfirmPassword] = useState('');
  const [savingPassword, setSavingPassword] = useState(false);
  const [passwordMsg, setPasswordMsg] = useState(null);

  const handleSavePassword = async () => {
    setPasswordMsg(null);
    if (newPassword !== confirmPassword) {
      setPasswordMsg({ ok: false, text: 'As senhas novas não coincidem.' });
      return;
    }
    setSavingPassword(true);
    try {
      await userService.updatePassword(currentPassword, newPassword);
      setCurrentPassword(''); setNewPassword(''); setConfirmPassword('');
      setPasswordMsg({ ok: true, text: 'Senha atualizada com sucesso.' });
    } catch (err) {
      setPasswordMsg({ ok: false, text: err.response?.data?.error || 'Erro ao atualizar senha.' });
    } finally {
      setSavingPassword(false);
    }
  };

  // ── Orçamentos por categoria ────────────────────────────────────────────
  const [categories, setCategories] = useState([]);
  const [limits, setLimits] = useState({});   // { [categoryId]: string }
  const [savingLimit, setSavingLimit] = useState(null); // categoryId em salvamento
  const [limitMsg, setLimitMsg] = useState({}); // { [categoryId]: { ok, text } }

  const [allCategories, setAllCategories] = useState([]);

  useEffect(() => {
    categoryService.list().then(res => {
      setAllCategories(res.data);
      const expenseCats = res.data.filter(c => c.type === 'expense');
      setCategories(expenseCats);
      setLimits(Object.fromEntries(expenseCats.map(c => [c.id, c.monthly_limit != null ? String(c.monthly_limit) : ''])));
    });
  }, []);

  const handleSaveLimit = async (categoryId) => {
    const raw = limits[categoryId];
    const value = raw === '' ? null : parseFloat(raw);
    if (value != null && (Number.isNaN(value) || value < 0)) return;
    setSavingLimit(categoryId);
    setLimitMsg(m => ({ ...m, [categoryId]: null }));
    try {
      await categoryService.updateMonthlyLimit(categoryId, value);
      setLimitMsg(m => ({ ...m, [categoryId]: { ok: true, text: 'Salvo.' } }));
    } catch (err) {
      setLimitMsg(m => ({ ...m, [categoryId]: { ok: false, text: err.response?.data?.error || 'Erro ao salvar.' } }));
    } finally {
      setSavingLimit(null);
    }
  };

  return (
    <div style={s.page}>
      <Header showBack />
      <div style={s.content}>

        {/* Perfil */}
        <div style={s.card}>
          <h3 style={s.title}>Perfil</h3>
          <div style={s.field}>
            <label style={s.label}>Nome</label>
            <div style={{ display: 'flex', gap: 8 }}>
              <input style={s.input} value={name} onChange={e => setName(e.target.value)} />
              <button style={s.btn()} disabled={savingName || name.trim() === user?.name} onClick={handleSaveName}>
                {savingName ? 'Salvando…' : 'Salvar'}
              </button>
            </div>
          </div>
          <div style={s.field}>
            <label style={s.label}>E-mail</label>
            <input style={{ ...s.input, opacity: 0.6, cursor: 'not-allowed' }} value={user?.email || ''} disabled />
          </div>
          {nameMsg && <p style={s.msg(nameMsg.ok)}>{nameMsg.text}</p>}

          <hr style={{ border: 'none', borderTop: '1px solid var(--border-light)', margin: '1.25rem 0' }} />

          <h3 style={{ ...s.title, marginBottom: '1rem' }}>Trocar senha</h3>
          <div style={s.field}>
            <label style={s.label}>Senha atual</label>
            <input type="password" style={s.input} value={currentPassword} onChange={e => setCurrentPassword(e.target.value)} />
          </div>
          <div style={s.field}>
            <label style={s.label}>Nova senha</label>
            <input type="password" style={s.input} value={newPassword} onChange={e => setNewPassword(e.target.value)} />
          </div>
          <div style={s.field}>
            <label style={s.label}>Confirmar nova senha</label>
            <input type="password" style={s.input} value={confirmPassword} onChange={e => setConfirmPassword(e.target.value)} />
          </div>
          <button style={s.btn()} disabled={savingPassword || !currentPassword || !newPassword} onClick={handleSavePassword}>
            {savingPassword ? 'Salvando…' : 'Atualizar senha'}
          </button>
          {passwordMsg && <p style={s.msg(passwordMsg.ok)}>{passwordMsg.text}</p>}
        </div>

        {/* Aparência */}
        <div style={s.card}>
          <h3 style={s.title}>Aparência</h3>
          <div style={{ display: 'flex', justifyContent: 'space-between', alignItems: 'center' }}>
            <span style={{ fontSize: '0.9rem', color: 'var(--text-primary)' }}>Tema {theme === 'dark' ? 'escuro' : 'claro'}</span>
            <button style={s.btn('var(--bg-subtle)', 'var(--text-primary)')} onClick={toggleTheme}>
              {theme === 'dark' ? '☀️ Usar tema claro' : '🌙 Usar tema escuro'}
            </button>
          </div>
        </div>

        {/* Orçamentos */}
        <div style={s.card}>
          <h3 style={s.title}>Orçamentos por categoria</h3>
          <p style={{ margin: '-0.75rem 0 1rem', fontSize: '0.8rem', color: 'var(--text-faint)' }}>
            Defina um teto mensal de gastos por categoria. Deixe em branco pra remover o teto.
          </p>
          {categories.length === 0 ? (
            <p style={{ color: 'var(--text-faint)', fontSize: '0.875rem' }}>Nenhuma categoria de despesa cadastrada.</p>
          ) : (
            <div style={{ display: 'flex', flexDirection: 'column', gap: 10 }}>
              {categories.map(c => {
                const original = c.monthly_limit != null ? String(c.monthly_limit) : '';
                const dirty = (limits[c.id] ?? '') !== original;
                const msg = limitMsg[c.id];
                return (
                  <div key={c.id} style={{ display: 'flex', alignItems: 'center', gap: 10, paddingBottom: 8, borderBottom: '1px solid var(--border-light)' }}>
                    <span style={{ width: 10, height: 10, borderRadius: '50%', background: c.color, flexShrink: 0 }} />
                    <span style={{ flex: 1, fontSize: '0.9rem', color: 'var(--text-primary)' }}>{c.name}</span>
                    <span style={{ fontSize: '0.85rem', color: 'var(--text-faint)' }}>R$</span>
                    <input
                      type="number" min="0" step="0.01" placeholder="Sem teto"
                      value={limits[c.id] ?? ''}
                      onChange={e => setLimits(l => ({ ...l, [c.id]: e.target.value }))}
                      style={{ ...s.input, width: 120 }}
                    />
                    <button
                      style={s.btn(dirty ? '#6366f1' : 'var(--bg-subtle)', dirty ? '#fff' : 'var(--text-faint)')}
                      disabled={!dirty || savingLimit === c.id}
                      onClick={() => handleSaveLimit(c.id)}
                    >
                      {savingLimit === c.id ? '…' : 'Salvar'}
                    </button>
                    {msg && <span style={{ ...s.msg(msg.ok), marginTop: 0 }}>{msg.text}</span>}
                  </div>
                );
              })}
            </div>
          )}
        </div>

        {/* Palavras-chave */}
        <KeywordsManager categories={allCategories} onCategoriesChange={setAllCategories} />

      </div>
    </div>
  );
}
