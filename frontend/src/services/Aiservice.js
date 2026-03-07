import axios from 'axios';

const api = axios.create({
    baseURL: process.env.REACT_APP_API_URL || '/api',
});

api.interceptors.request.use((config) => {
    const token = localStorage.getItem('token');
    if (token) config.headers.Authorization = `Bearer ${token}`;
    return config;
});

api.interceptors.response.use(
    (res) => res,
    (err) => {
        if (err.response?.status === 401) {
            localStorage.removeItem('token');
            localStorage.removeItem('user');
            window.location.href = '/login';
        }
        return Promise.reject(err);
    }
);

export const aiService = {
    listChats: () => api.get('/ai/chats'),
    createChat: (title) => api.post('/ai/chats', { title }),
    deleteChat: (id) => api.delete(`/ai/chats/${id}`),
    getMessages: (chatId) => api.get(`/ai/chats/${chatId}/messages`),
    sendMessage: (chatId, content) =>
        api.post(`/ai/chats/${chatId}/messages`, { content }),
};

export default api;