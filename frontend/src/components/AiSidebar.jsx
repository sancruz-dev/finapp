import React, { useState, useEffect, useRef, useCallback } from 'react';
import { aiService } from '../services/Aiservice.js';

// ── Helpers ────────────────────────────────────────────────────────────────

function timeAgo(dateStr) {
  const diff = Date.now() - new Date(dateStr).getTime();
  const m = Math.floor(diff / 60000);
  if (m < 1) return 'agora';
  if (m < 60) return `${m}m atrás`;
  const h = Math.floor(m / 60);
  if (h < 24) return `${h}h atrás`;
  return `${Math.floor(h / 24)}d atrás`;
}

function TypingIndicator() {
  return (
    <div style={msgStyles.assistantRow}>
      <div style={msgStyles.avatar}>✦</div>
      <div style={{ ...msgStyles.bubble, ...msgStyles.assistantBubble, padding: '12px 16px' }}>
        <div style={styles.typingDots}>
          <span /><span /><span />
        </div>
      </div>
    </div>
  );
}

function MessageBubble({ msg }) {
  const isUser = msg.role === 'user';
  return (
    <div style={isUser ? msgStyles.userRow : msgStyles.assistantRow}>
      {!isUser && <div style={msgStyles.avatar}>✦</div>}
      <div style={{
        ...msgStyles.bubble,
        ...(isUser ? msgStyles.userBubble : msgStyles.assistantBubble),
      }}>
        <p style={msgStyles.text}>{msg.content}</p>
        {msg.created_at && (
          <span style={msgStyles.time}>{timeAgo(msg.created_at)}</span>
        )}
      </div>
      {isUser && <div style={msgStyles.userAvatar}>eu</div>}
    </div>
  );
}

// ── Estilos ────────────────────────────────────────────────────────────────

const styles = {
  sidebar: {
    width: 340,
    minWidth: 340,
    height: '100vh',
    position: 'sticky',
    top: 0,
    display: 'flex',
    flexDirection: 'column',
    background: 'linear-gradient(180deg, #0f0f1a 0%, #13131f 60%, #0f0f1a 100%)',
    borderLeft: '1px solid rgba(99,102,241,0.15)',
    fontFamily: "'Segoe UI', system-ui, sans-serif",
    overflow: 'hidden',
    boxShadow: '-4px 0 24px rgba(0,0,0,0.3)',
  },

  header: {
    padding: '1.25rem 1.25rem 1rem',
    borderBottom: '1px solid rgba(255,255,255,0.06)',
    background: 'rgba(99,102,241,0.06)',
    backdropFilter: 'blur(10px)',
    flexShrink: 0,
  },

  headerTop: {
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    marginBottom: '0.875rem',
  },

  headerTitle: {
    display: 'flex',
    alignItems: 'center',
    gap: 8,
  },

  titleIcon: {
    width: 28,
    height: 28,
    borderRadius: 8,
    background: 'linear-gradient(135deg, #6366f1, #818cf8)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '0.85rem',
    boxShadow: '0 2px 8px rgba(99,102,241,0.4)',
  },

  titleText: {
    fontSize: '0.9rem',
    fontWeight: 700,
    color: '#e2e8f0',
    letterSpacing: '-0.2px',
  },

  titleSub: {
    fontSize: '0.7rem',
    color: 'rgba(148,163,184,0.7)',
    marginTop: 1,
  },

  newChatBtn: {
    background: 'rgba(99,102,241,0.15)',
    border: '1px solid rgba(99,102,241,0.3)',
    borderRadius: 8,
    color: '#818cf8',
    fontSize: '0.75rem',
    fontWeight: 600,
    padding: '6px 12px',
    cursor: 'pointer',
    display: 'flex',
    alignItems: 'center',
    gap: 4,
    transition: 'all 0.15s',
  },

  chatList: {
    flex: '0 0 auto',
    maxHeight: 200,
    overflowY: 'auto',
    padding: '0.5rem 0.75rem',
    borderBottom: '1px solid rgba(255,255,255,0.05)',
  },

  chatListLabel: {
    fontSize: '0.65rem',
    fontWeight: 700,
    color: 'rgba(148,163,184,0.5)',
    letterSpacing: '0.08em',
    textTransform: 'uppercase',
    padding: '0.5rem 0.5rem 0.375rem',
  },

  chatItem: (active) => ({
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'space-between',
    padding: '7px 10px',
    borderRadius: 8,
    cursor: 'pointer',
    marginBottom: 2,
    background: active ? 'rgba(99,102,241,0.18)' : 'transparent',
    border: `1px solid ${active ? 'rgba(99,102,241,0.35)' : 'transparent'}`,
    transition: 'all 0.12s',
    gap: 6,
  }),

  chatItemTitle: (active) => ({
    flex: 1,
    fontSize: '0.8rem',
    color: active ? '#c7d2fe' : 'rgba(203,213,225,0.75)',
    fontWeight: active ? 600 : 400,
    overflow: 'hidden',
    textOverflow: 'ellipsis',
    whiteSpace: 'nowrap',
  }),

  chatItemTime: {
    fontSize: '0.65rem',
    color: 'rgba(148,163,184,0.45)',
    flexShrink: 0,
  },

  deleteBtn: {
    background: 'none',
    border: 'none',
    cursor: 'pointer',
    color: 'rgba(148,163,184,0.3)',
    fontSize: '0.75rem',
    padding: '2px 4px',
    borderRadius: 4,
    flexShrink: 0,
    lineHeight: 1,
    transition: 'color 0.12s',
  },

  messagesArea: {
    flex: 1,
    overflowY: 'auto',
    padding: '1rem 1rem 0.5rem',
    display: 'flex',
    flexDirection: 'column',
    gap: 2,
    scrollbarWidth: 'thin',
    scrollbarColor: 'rgba(99,102,241,0.2) transparent',
  },

  emptyState: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '2rem 1.5rem',
    gap: 16,
  },

  emptyIcon: {
    width: 52,
    height: 52,
    borderRadius: 16,
    background: 'linear-gradient(135deg, rgba(99,102,241,0.2), rgba(129,140,248,0.1))',
    border: '1px solid rgba(99,102,241,0.2)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '1.5rem',
  },

  emptyTitle: {
    fontSize: '0.9rem',
    fontWeight: 700,
    color: '#e2e8f0',
    textAlign: 'center',
    margin: 0,
  },

  emptyDesc: {
    fontSize: '0.78rem',
    color: 'rgba(148,163,184,0.6)',
    textAlign: 'center',
    lineHeight: 1.5,
    margin: 0,
  },

  suggestions: {
    display: 'flex',
    flexDirection: 'column',
    gap: 6,
    width: '100%',
    marginTop: 4,
  },

  suggestion: {
    background: 'rgba(255,255,255,0.04)',
    border: '1px solid rgba(255,255,255,0.08)',
    borderRadius: 10,
    padding: '8px 12px',
    cursor: 'pointer',
    textAlign: 'left',
    color: 'rgba(203,213,225,0.8)',
    fontSize: '0.78rem',
    lineHeight: 1.4,
    transition: 'all 0.12s',
  },

  inputArea: {
    padding: '0.875rem',
    borderTop: '1px solid rgba(255,255,255,0.06)',
    background: 'rgba(0,0,0,0.2)',
    flexShrink: 0,
  },

  inputWrapper: {
    display: 'flex',
    alignItems: 'flex-end',
    gap: 8,
    background: 'rgba(255,255,255,0.05)',
    border: '1px solid rgba(99,102,241,0.2)',
    borderRadius: 12,
    padding: '8px 8px 8px 12px',
    transition: 'border-color 0.15s',
  },

  textarea: {
    flex: 1,
    background: 'transparent',
    border: 'none',
    outline: 'none',
    color: '#e2e8f0',
    fontSize: '0.85rem',
    lineHeight: 1.5,
    resize: 'none',
    fontFamily: 'inherit',
    maxHeight: 100,
    minHeight: 20,
  },

  sendBtn: (disabled) => ({
    width: 32,
    height: 32,
    borderRadius: 8,
    background: disabled ? 'rgba(99,102,241,0.2)' : 'linear-gradient(135deg, #6366f1, #818cf8)',
    border: 'none',
    cursor: disabled ? 'not-allowed' : 'pointer',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    color: disabled ? 'rgba(255,255,255,0.3)' : '#fff',
    flexShrink: 0,
    transition: 'all 0.15s',
    boxShadow: disabled ? 'none' : '0 2px 8px rgba(99,102,241,0.35)',
  }),

  noChat: {
    flex: 1,
    display: 'flex',
    flexDirection: 'column',
    alignItems: 'center',
    justifyContent: 'center',
    padding: '2rem',
    gap: 12,
  },

  typingDots: {
    display: 'flex',
    gap: 4,
    alignItems: 'center',
  },
};

const msgStyles = {
  assistantRow: {
    display: 'flex',
    gap: 8,
    alignItems: 'flex-start',
    marginBottom: 10,
  },
  userRow: {
    display: 'flex',
    gap: 8,
    alignItems: 'flex-start',
    justifyContent: 'flex-end',
    marginBottom: 10,
  },
  avatar: {
    width: 26,
    height: 26,
    borderRadius: 8,
    background: 'linear-gradient(135deg, #6366f1, #818cf8)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '0.7rem',
    color: '#fff',
    flexShrink: 0,
    marginTop: 2,
    boxShadow: '0 2px 6px rgba(99,102,241,0.3)',
  },
  userAvatar: {
    width: 26,
    height: 26,
    borderRadius: 8,
    background: 'rgba(255,255,255,0.08)',
    display: 'flex',
    alignItems: 'center',
    justifyContent: 'center',
    fontSize: '0.6rem',
    color: 'rgba(203,213,225,0.6)',
    flexShrink: 0,
    marginTop: 2,
    fontWeight: 700,
    letterSpacing: '0.02em',
  },
  bubble: {
    maxWidth: '80%',
    borderRadius: 12,
    padding: '10px 13px',
  },
  assistantBubble: {
    background: 'rgba(255,255,255,0.05)',
    border: '1px solid rgba(255,255,255,0.08)',
    borderTopLeftRadius: 4,
  },
  userBubble: {
    background: 'rgba(99,102,241,0.25)',
    border: '1px solid rgba(99,102,241,0.3)',
    borderTopRightRadius: 4,
  },
  text: {
    margin: 0,
    fontSize: '0.82rem',
    color: '#e2e8f0',
    lineHeight: 1.6,
    whiteSpace: 'pre-wrap',
    wordBreak: 'break-word',
  },
  time: {
    display: 'block',
    fontSize: '0.65rem',
    color: 'rgba(148,163,184,0.4)',
    marginTop: 4,
    textAlign: 'right',
  },
};

// ── Sugestões de perguntas ─────────────────────────────────────────────────

const SUGGESTIONS = [
  '💸 Como estão meus gastos esse mês?',
  '📈 Qual categoria mais cresceu nos últimos 3 meses?',
  '🎯 Se continuar assim, como vai ficar meu saldo?',
  '📊 Compare meus gastos com a média histórica',
];

// ── Componente principal ───────────────────────────────────────────────────

export default function AiSidebar() {
  const [chats, setChats] = useState([]);
  const [activeChatId, setActiveChatId] = useState(null);
  const [messages, setMessages] = useState([]);
  const [input, setInput] = useState('');
  const [loading, setLoading] = useState(false);
  const [loadingChats, setLoadingChats] = useState(true);
  const messagesEndRef = useRef(null);
  const textareaRef = useRef(null);

  // Carrega lista de chats
  const loadChats = useCallback(async () => {
    try {
      const res = await aiService.listChats();
      setChats(res.data);
    } catch {
      // silencioso
    } finally {
      setLoadingChats(false);
    }
  }, []);

  useEffect(() => { loadChats(); }, [loadChats]);

  // Scroll automático
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: 'smooth' });
  }, [messages, loading]);

  // Seleciona chat e carrega mensagens
  const selectChat = async (chatId) => {
    if (activeChatId === chatId) return;
    setActiveChatId(chatId);
    setMessages([]);
    try {
      const res = await aiService.getMessages(chatId);
      setMessages(res.data);
    } catch {
      setMessages([]);
    }
  };

  // Cria novo chat
  const newChat = async () => {
    try {
      const res = await aiService.createChat(null);
      const chat = res.data;
      setChats(prev => [{ id: chat.id, title: chat.title, updated_at: new Date().toISOString() }, ...prev]);
      setActiveChatId(chat.id);
      setMessages([]);
    } catch {/* silencioso */}
  };

  // Deleta chat
  const deleteChat = async (e, chatId) => {
    e.stopPropagation();
    try {
      await aiService.deleteChat(chatId);
      setChats(prev => prev.filter(c => c.id !== chatId));
      if (activeChatId === chatId) {
        setActiveChatId(null);
        setMessages([]);
      }
    } catch {/* silencioso */}
  };

  // Envia mensagem
  const sendMessage = async (content) => {
    const text = content ?? input.trim();
    if (!text || loading) return;

    let chatId = activeChatId;

    // Cria chat automaticamente se não tiver um ativo
    if (!chatId) {
      try {
        const res = await aiService.createChat(null);
        chatId = res.data.id;
        setActiveChatId(chatId);
        setChats(prev => [{ id: chatId, title: 'Nova conversa', updated_at: new Date().toISOString() }, ...prev]);
      } catch { return; }
    }

    const userMsg = { role: 'user', content: text, created_at: new Date().toISOString() };
    setMessages(prev => [...prev, userMsg]);
    setInput('');
    setLoading(true);

    // Reseta altura do textarea
    if (textareaRef.current) textareaRef.current.style.height = 'auto';

    try {
      const res = await aiService.sendMessage(chatId, text);
      setMessages(prev => [...prev, res.data]);

      // Atualiza título do chat na lista
      setChats(prev => prev.map(c =>
        c.id === chatId
          ? { ...c, title: text.length > 50 ? text.slice(0, 47) + '...' : text, updated_at: new Date().toISOString() }
          : c
      ));
    } catch {
      setMessages(prev => [...prev, {
        role: 'assistant',
        content: 'Erro ao conectar com o assistente. Verifique se o Ollama está rodando.',
        created_at: new Date().toISOString(),
      }]);
    } finally {
      setLoading(false);
    }
  };

  const handleKeyDown = (e) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      sendMessage();
    }
  };

  const handleTextareaChange = (e) => {
    setInput(e.target.value);
    // Auto-resize
    e.target.style.height = 'auto';
    e.target.style.height = Math.min(e.target.scrollHeight, 100) + 'px';
  };

  const hasMessages = messages.length > 0;

  return (
    <>
      {/* CSS para animações */}
      <style>{`
        @keyframes fadeSlideUp {
          from { opacity: 0; transform: translateY(8px); }
          to   { opacity: 1; transform: translateY(0); }
        }
        @keyframes pulse {
          0%, 80%, 100% { transform: scale(0.6); opacity: 0.4; }
          40%            { transform: scale(1);   opacity: 1; }
        }
        .ai-chat-item:hover {
          background: rgba(99,102,241,0.1) !important;
          border-color: rgba(99,102,241,0.2) !important;
        }
        .ai-delete-btn:hover { color: #f87171 !important; }
        .ai-new-chat-btn:hover {
          background: rgba(99,102,241,0.25) !important;
          border-color: rgba(99,102,241,0.5) !important;
          color: #a5b4fc !important;
        }
        .ai-suggestion:hover {
          background: rgba(99,102,241,0.1) !important;
          border-color: rgba(99,102,241,0.25) !important;
          color: #c7d2fe !important;
        }
        .ai-input-wrapper:focus-within {
          border-color: rgba(99,102,241,0.5) !important;
          box-shadow: 0 0 0 3px rgba(99,102,241,0.08) !important;
        }
        .ai-msg { animation: fadeSlideUp 0.2s ease-out; }
        .ai-typing-dot {
          width: 5px; height: 5px; border-radius: 50%;
          background: rgba(129,140,248,0.7);
          animation: pulse 1.4s infinite ease-in-out;
        }
        .ai-typing-dot:nth-child(2) { animation-delay: 0.2s; }
        .ai-typing-dot:nth-child(3) { animation-delay: 0.4s; }
        .ai-messages-area::-webkit-scrollbar { width: 4px; }
        .ai-messages-area::-webkit-scrollbar-track { background: transparent; }
        .ai-messages-area::-webkit-scrollbar-thumb { background: rgba(99,102,241,0.2); border-radius: 2px; }
        .ai-chat-list::-webkit-scrollbar { width: 3px; }
        .ai-chat-list::-webkit-scrollbar-track { background: transparent; }
        .ai-chat-list::-webkit-scrollbar-thumb { background: rgba(255,255,255,0.08); border-radius: 2px; }
      `}</style>

      <aside style={styles.sidebar}>

        {/* ── Header ── */}
        <div style={styles.header}>
          <div style={styles.headerTop}>
            <div style={styles.headerTitle}>
              <div style={styles.titleIcon}>✦</div>
              <div>
                <div style={styles.titleText}>Assistente IA</div>
                <div style={styles.titleSub}>Análise financeira inteligente</div>
              </div>
            </div>
            <button
              className="ai-new-chat-btn"
              onClick={newChat}
              style={styles.newChatBtn}
              title="Nova conversa"
            >
              + Novo
            </button>
          </div>
        </div>

        {/* ── Lista de chats ── */}
        {!loadingChats && chats.length > 0 && (
          <div className="ai-chat-list" style={styles.chatList}>
            <div style={styles.chatListLabel}>Conversas</div>
            {chats.map(chat => (
              <div
                key={chat.id}
                className="ai-chat-item"
                style={styles.chatItem(activeChatId === chat.id)}
                onClick={() => selectChat(chat.id)}
              >
                <span style={styles.chatItemTitle(activeChatId === chat.id)}>
                  {chat.title}
                </span>
                <span style={styles.chatItemTime}>{timeAgo(chat.updated_at)}</span>
                <button
                  className="ai-delete-btn"
                  style={styles.deleteBtn}
                  onClick={(e) => deleteChat(e, chat.id)}
                  title="Remover conversa"
                >✕</button>
              </div>
            ))}
          </div>
        )}

        {/* ── Área de mensagens ── */}
        <div className="ai-messages-area" style={styles.messagesArea}>
          {!hasMessages && !loading ? (
            <div style={styles.emptyState}>
              <div style={styles.emptyIcon}>✦</div>
              <p style={styles.emptyTitle}>O que quer saber hoje?</p>
              <p style={styles.emptyDesc}>
                Analiso seus dados financeiros em tempo real e respondo em português.
              </p>
              <div style={styles.suggestions}>
                {SUGGESTIONS.map((s, i) => (
                  <button
                    key={i}
                    className="ai-suggestion"
                    style={styles.suggestion}
                    onClick={() => sendMessage(s)}
                  >
                    {s}
                  </button>
                ))}
              </div>
            </div>
          ) : (
            <>
              {messages.map((msg, i) => (
                <div key={i} className="ai-msg">
                  <MessageBubble msg={msg} />
                </div>
              ))}
              {loading && <TypingIndicator />}
            </>
          )}
          <div ref={messagesEndRef} />
        </div>

        {/* ── Input ── */}
        <div style={styles.inputArea}>
          <div className="ai-input-wrapper" style={styles.inputWrapper}>
            <textarea
              ref={textareaRef}
              value={input}
              onChange={handleTextareaChange}
              onKeyDown={handleKeyDown}
              placeholder="Pergunte sobre suas finanças..."
              rows={1}
              disabled={loading}
              style={{
                ...styles.textarea,
                opacity: loading ? 0.5 : 1,
              }}
            />
            <button
              style={styles.sendBtn(!input.trim() || loading)}
              onClick={() => sendMessage()}
              disabled={!input.trim() || loading}
              title="Enviar (Enter)"
            >
              <svg width="14" height="14" viewBox="0 0 24 24" fill="currentColor">
                <path d="M2.01 21L23 12 2.01 3 2 10l15 2-15 2z"/>
              </svg>
            </button>
          </div>
          <p style={{
            margin: '6px 2px 0',
            fontSize: '0.65rem',
            color: 'rgba(148,163,184,0.35)',
          }}>
            Enter para enviar · Shift+Enter para nova linha
          </p>
        </div>

      </aside>
    </>
  );
}