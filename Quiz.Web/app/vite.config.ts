import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react-swc'
import tailwindcss from '@tailwindcss/vite'

export default defineConfig({
    plugins: [
        react(),
        tailwindcss()
    ],
    server: {
        port: 4000,
        proxy: {
            '/api': {
                target: 'http://localhost:5000',
                changeOrigin: true,
                rewrite: (path) => path.replace(/^\/api/, ''),
            },
            '/hubs': {
                target: 'http://localhost:5000',
                changeOrigin: true,
                ws: true, // Enable WebSocket proxy for SignalR
            },
        },
    },
    preview: {
        port: 4000,
    },
})
