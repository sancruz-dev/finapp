import { useState, useEffect, useCallback } from 'react';
import { transactionService } from '../services/api';

const ISO_DATE_RE = /^\d{4}-\d{2}-\d{2}$/;

/**
 * period: { mode: 'auto', month, year } | { mode: 'range', startDate, endDate }
 * (startDate/endDate no formato 'YYYY-MM-DD')
 */
export function useTransactions(period) {
  const [transactions, setTransactions] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  const isRange = period?.mode === 'range';
  // Enquanto o usuário digita a data manualmente (em vez de usar o calendário),
  // o input pode passar por valores incompletos/vazios — não busca até estar completo.
  const isValid = isRange
    ? ISO_DATE_RE.test(period.startDate) && ISO_DATE_RE.test(period.endDate)
    : Boolean(period?.month) && Boolean(period?.year);

  const params = isRange
    ? { start_date: period.startDate, end_date: period.endDate }
    : { month: period?.month, year: period?.year };

  const paramsKey = JSON.stringify(params);

  const fetch = useCallback(async () => {
    if (!isValid) return;
    setLoading(true);
    try {
      const [txRes, sumRes] = await Promise.all([
        transactionService.list(params),
        transactionService.summary(params),
      ]);
      setTransactions(txRes.data);
      setSummary(sumRes.data);
    } finally {
      setLoading(false);
    }
    // eslint-disable-next-line
  }, [paramsKey, isValid]);

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
