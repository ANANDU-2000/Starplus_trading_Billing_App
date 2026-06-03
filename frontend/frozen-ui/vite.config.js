import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

const buildStamp = process.env.VITE_APP_BUILD || new Date().toISOString().slice(0, 10)

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react()],
  define: {
    'import.meta.env.VITE_APP_BUILD': JSON.stringify(buildStamp)
  },
  resolve: {
    dedupe: ['react', 'react-dom']
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true,
      },
    },
  },
})
