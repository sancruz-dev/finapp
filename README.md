# 💰 FinApp — Gestão Financeira Pessoal

Stack: React · .NET 8 (ASP.NET Core Web API) · MySQL · ML.NET · Semantic Kernel (Groq / Ollama)

---

## ✨ Funcionalidades

- **Gestão de transações**: CRUD completo, resumo mensal, categorização por palavra-chave
- **Importação via XLSX**: upload de planilhas com fluxo de preview e confirmação (ClosedXML)
- **Categorização inteligente de estabelecimentos (ML)**: normalização de nomes de merchants, predição automática de categoria via ML.NET, treinamento por usuário, backfill histórico e fila de revisão para casos de baixa confiança
- **Assistente de IA (chat)**: chat contextualizado com os dados financeiros do usuário (Semantic Kernel), com suporte a **Groq** (nuvem) ou **Ollama** (modelo local)
- **Tema Dark/Light**: alternância de tema persistida no navegador
- **Autenticação JWT**: login/registro com senha criptografada (BCrypt)

---

## 🗄️ 1. Banco de dados

```bash
mysql -u root -p < backend/Database/Scripts/schema.sql
mysql -u root -p < backend/Database/Scripts/schema_merchants.sql
mysql -u root -p < backend/Database/Scripts/migration_add_method.sql
# opcional: dados de exemplo
mysql -u root -p < backend/Database/Scripts/seed_finapp.sql
```

Isso cria o banco `finapp` com as tabelas de transações, categorias, merchants e categorias padrão.

---

## ⚙️ 2. Backend (.NET 8)

```bash
cd backend
cp .env.example .env
# Edite o .env com sua senha do MySQL, um JWT_SECRET seguro e as chaves de IA

dotnet restore
dotnet run
# Rodando em http://localhost:3001
```

### Variáveis de ambiente (`.env`)
| Variável | Descrição |
|---|---|
| `PORT` | Porta do backend (padrão 3001) |
| `DB_HOST`, `DB_PORT`, `DB_USER`, `DB_PASSWORD`, `DB_NAME` | Conexão MySQL |
| `JWT_SECRET`, `JWT_EXPIRES_IN` | Autenticação JWT |
| `AI_PROVIDER` | `groq` (padrão) ou `ollama` |
| `GROQ_API_KEY`, `GROQ_MODEL` | Necessário quando `AI_PROVIDER=groq` |
| `OLLAMA_URL`, `OLLAMA_MODEL` | Necessário quando `AI_PROVIDER=ollama` (padrão `http://localhost:11434`, modelo `phi3`) |

### Endpoints disponíveis
| Método | Rota | Descrição |
|--------|------|-----------|
| POST | /api/auth/login | Login |
| POST | /api/auth/register | Cadastro |
| GET | /api/transactions | Listar transações |
| POST | /api/transactions | Criar transação |
| PUT | /api/transactions/:id | Atualizar |
| DELETE | /api/transactions/:id | Remover |
| GET | /api/transactions/summary | Resumo do mês |
| GET | /api/categories | Listar categorias |
| POST | /api/categories | Criar categoria |
| DELETE | /api/categories/:id | Remover categoria |
| POST | /api/import/preview | Pré-visualizar importação de XLSX |
| POST | /api/import/confirm | Confirmar importação de XLSX |
| GET/POST/DELETE | /api/merchants | CRUD de merchants e aliases |
| POST | /api/merchants/backfill | Reprocessar transações antigas com ML |
| GET | /api/merchants/review-queue | Fila de revisão de predições incertas |
| POST | /api/merchants/review-queue/:id/resolve | Resolver item da fila |
| POST | /api/merchants/predict | Prever categoria de um merchant |
| POST | /api/merchants/retrain | Retreinar o modelo ML do usuário |
| GET/POST/DELETE | /api/ai/chats | Gerenciar conversas de IA |
| GET/POST | /api/ai/chats/:id/messages | Mensagens do chat |
| GET | /api/ai/debug/context | Debug do contexto financeiro enviado à IA |

---

## 🎨 3. Frontend

```bash
cd frontend
npm install
npm start
# Abre em http://localhost:3000
```

O `proxy` no package.json já aponta para `http://localhost:3001`, então não precisa configurar CORS para desenvolvimento local.

### Principais telas
- **Login** — autenticação
- **Dashboard** — resumo financeiro, gráficos (Recharts) e transações
- **Importação** — upload e confirmação de extratos em XLSX
- **Merchants** — revisão e gestão da categorização automática (ML)
- **Assistente de IA** — chat lateral com contexto financeiro do usuário
- Alternância de **tema claro/escuro** no header

---

## 👥 Acesso da segunda pessoa (mesma rede)

No computador onde roda o backend/frontend, descubra o IP local:
```bash
# Linux/Mac
ip addr show | grep "inet "

# Windows
ipconfig
```

A segunda pessoa acessa: `http://SEU_IP:3000`

Para isso funcionar, o frontend precisa saber o endereço do backend:
```bash
# frontend/.env.local
REACT_APP_API_URL=http://SEU_IP:3001/api
```

---

## 🔑 Criando os usuários

Após subir o backend, registre os dois usuários pelo endpoint ou crie diretamente no banco:

```bash
# Via curl
curl -X POST http://localhost:3001/api/auth/register \
  -H "Content-Type: application/json" \
  -d '{"name":"Nome","email":"user@email.com","password":"senha123"}'
```

Ou use Postman/Insomnia.

---

## 🗂️ Estrutura do projeto

```
finapp/
├── backend/
│   ├── FinApp.Api.csproj
│   ├── Program.cs
│   ├── appsettings.json
│   ├── .env.example
│   ├── Database/
│   │   └── Scripts/
│   │       ├── schema.sql
│   │       ├── schema_merchants.sql
│   │       ├── migration_add_method.sql
│   │       └── seed_finapp.sql
│   ├── Controllers/
│   │   ├── AuthController.cs
│   │   ├── TransactionController.cs
│   │   ├── CategoryController.cs
│   │   ├── ImportController.cs
│   │   ├── MerchantsController.cs
│   │   └── AiController.cs
│   ├── Services/
│   │   ├── AuthService.cs / JwtService.cs
│   │   ├── TransactionService.cs / CategoryService.cs
│   │   ├── ImportService.cs
│   │   ├── MerchantNormalizerService.cs   # ML.NET: predição, treino, backfill
│   │   ├── AiService.cs                   # Chat IA (Groq/Ollama)
│   │   └── FinancialContextPlugin.cs      # Contexto financeiro p/ Semantic Kernel
│   ├── Models/
│   └── Data/
└── frontend/
    ├── package.json
    └── src/
        ├── App.jsx
        ├── index.js
        ├── context/
        │   ├── AuthContext.jsx
        │   └── ThemeContext.jsx
        ├── hooks/useTransactions.js
        ├── services/
        │   ├── api.js
        │   └── Aiservice.js
        ├── pages/
        │   ├── Login.jsx
        │   ├── Dashboard.jsx
        │   ├── ImportPage.jsx
        │   └── MerchantsPage.jsx
        └── components/
            ├── Header.jsx
            ├── AiSidebar.jsx
            ├── Markdown.jsx
            └── TransactionModal.jsx
```

