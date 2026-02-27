const express = require('express');
const router = express.Router();
const auth = require('../middleware/auth');
const authCtrl = require('../controllers/authController');
const txCtrl = require('../controllers/transactionController');
const catCtrl = require('../controllers/categoryController');

// Auth
router.post('/auth/login', authCtrl.login);
router.post('/auth/register', authCtrl.register);

// Transactions (protegidas)
router.get('/transactions', auth, txCtrl.list);
router.post('/transactions', auth, txCtrl.create);
router.put('/transactions/:id', auth, txCtrl.update);
router.delete('/transactions/:id', auth, txCtrl.remove);
router.get('/transactions/summary', auth, txCtrl.summary);

// Categories (protegidas)
router.get('/categories', auth, catCtrl.list);
router.post('/categories', auth, catCtrl.create);
router.delete('/categories/:id', auth, catCtrl.remove);

module.exports = router;
