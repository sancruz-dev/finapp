import { useState, useEffect, useCallback } from 'react';
import { transactionService } from '../services/api';
import dayjs from 'dayjs';

export function useTransactions(month, year) {
  const [transactions, setTransactions] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  const fetch = useCallback(async () => {
    setLoading(true);
    try {
      const [txRes, sumRes] = await Promise.all([
        transactionService.list({ month, year }),
        transactionService.summary({ month, year }),
      ]);
      setTransactions(txRes.data);
      setSummary(sumRes.data);
    } finally {
      setLoading(false);
    }
  }, [month, year]);

  useEffect(() => { fetch(); }, [fetch]);

  const add = async (data) => {
    await transactionService.create(data);
    fetch();
  };

  const update = async (id, data) => {
    await transactionService.update(id, data);
    fetch();
  };

  const remove = async (id) => {
    await transactionService.remove(id);
    fetch();
  };

  return { transactions, summary, loading, add, update, remove, refresh: fetch };
}
