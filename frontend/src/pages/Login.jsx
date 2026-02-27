import React, { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuth } from '../context/AuthContext';

export default function Login() {
  const [email, setEmail] = useState('');
  const [password, setPassword] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const { login } = useAuth();
  const navigate = useNavigate();

  const handleSubmit = async (e) => {
    e.preventDefault();
    setError('');
    setLoading(true);
    try {
      await login(email, password);
      navigate('/');
    } catch (err) {
      setError(err.response?.data?.error || 'Erro ao fazer login');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div style={{ minHeight: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', background: '#f8fafc' }}>
      <div style={{ background: '#fff', padding: '2rem', borderRadius: '12px', boxShadow: '0 2px 16px #0001', width: 360 }}>
        <h2 style={{ marginBottom: '1.5rem', textAlign: 'center' }}>💰 FinApp</h2>
        <form onSubmit={handleSubmit}>
          <div style={{ marginBottom: '1rem' }}>
            <label>Email</label>
            <input
              type="email" value={email} onChange={e => setEmail(e.target.value)}
              required style={{ display: 'block', width: '100%', padding: '8px', marginTop: 4, borderRadius: 6, border: '1px solid #e2e8f0' }}
            />
          </div>
          <div style={{ marginBottom: '1rem' }}>
            <label>Senha</label>
            <input
              type="password" value={password} onChange={e => setPassword(e.target.value)}
              required style={{ display: 'block', width: '100%', padding: '8px', marginTop: 4, borderRadius: 6, border: '1px solid #e2e8f0' }}
            />
          </div>
          {error && <p style={{ color: '#ef4444', marginBottom: '1rem' }}>{error}</p>}
          <button type="submit" disabled={loading}
            style={{ width: '100%', padding: '10px', background: '#6366f1', color: '#fff', border: 'none', borderRadius: 6, cursor: 'pointer', fontWeight: 600 }}>
            {loading ? 'Entrando...' : 'Entrar'}
          </button>
        </form>
      </div>
    </div>
  );
}
