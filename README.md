# 💰 FinApp — Gestão Financeira Pessoal

Stack: React · Node.js/Express · MySQL

---

## 🗄️ 1. Banco de dados

```bash
mysql -u root -p < backend/schema.sql
```

Isso cria o banco `finapp` com todas as tabelas e categorias padrão.

---

## ⚙️ 2. Backend

```bash
cd backend
cp .env.example .env
# Edite o .env com sua senha do MySQL e um JWT_SECRET seguro

npm install
npm run dev
# Rodando em http://localhost:3001
```

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

---

## 🎨 3. Frontend

```bash
cd frontend
npm install
npm start
# Abre em http://localhost:3000
```

O `proxy` no package.json já aponta para `http://localhost:3001`, então não precisa configurar CORS para desenvolvimento local.

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
│   ├── schema.sql
│   ├── .env.example
│   ├── package.json
│   └── src/
│       ├── server.js
│       ├── db.js
│       ├── middleware/auth.js
│       ├── controllers/
│       │   ├── authController.js
│       │   ├── transactionController.js
│       │   └── categoryController.js
│       └── routes/index.js
└── frontend/
    ├── package.json
    └── src/
        ├── App.jsx
        ├── index.js
        ├── context/AuthContext.jsx
        ├── hooks/useTransactions.js
        ├── services/api.js
        ├── pages/
        │   ├── Login.jsx
        │   └── Dashboard.jsx
        └── components/
            └── TransactionModal.jsx
```

---

## 🚀 Próximos passos sugeridos

- [ ] Página de categorias (CRUD visual)
- [ ] Filtros por tipo e categoria na listagem
- [ ] Gráfico de evolução mensal (linha, últimos 6 meses)
- [ ] Exportar para CSV
- [ ] Modo escuro
- [ ] PWA para acessar pelo celular
