require('dotenv').config();
const express = require('express');
const cors = require('cors');
const routes = require('./routes');

const app = express();

app.use(cors({ origin: '*' })); // Para rede local — restrinja em produção
app.use(express.json());
app.use('/api', routes);

app.get('/health', (_, res) => res.json({ status: 'ok' }));

const PORT = process.env.PORT || 3001;
app.listen(PORT, '0.0.0.0', () => {
  console.log(`🚀 Backend rodando em http://0.0.0.0:${PORT}`);
});
