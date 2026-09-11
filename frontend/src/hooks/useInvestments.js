import { useState, useEffect, useCallback } from 'react';
import { investmentService } from '../services/api';

export function useInvestments() {
  const [investments, setInvestments] = useState([]);
  const [summary, setSummary] = useState(null);
  const [loading, setLoading] = useState(true);

  const fetch = useCallback(async () => {
    setLoading(true);
    try {
      const [invRes, sumRes] = await Promise.all([
        investmentService.list(),
        investmentService.summary(),
      ]);
      setInvestments(invRes.data);
      setSummary(sumRes.data);
    } finally {
      setLoading(false);
    }
  }, []);

  useEffect(() => { fetch(); }, [fetch]);

  const add = async (data) => {
    await investmentService.create(data);
    fetch();
  };

  const update = async (id, data) => {
    await investmentService.update(id, data);
    fetch();
  };

  const remove = async (id) => {
    await investmentService.remove(id);
    fetch();
  };

  const addMovement = async (id, data) => {
    await investmentService.addMovement(id, data);
    fetch();
  };

  return { investments, summary, loading, add, update, remove, addMovement, refresh: fetch };
}
