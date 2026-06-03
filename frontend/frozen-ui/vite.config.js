import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import { writeFileSync } from 'fs'
import { resolve } from 'path'

const buildStamp = process.env.VITE_APP_BUILD || new Date().toISOString()
const commitRef = process.env.COMMIT_REF || process.env.GITHUB_SHA?.slice(0, 7) || 'local'

function emitVersionJson () {
  return {
    name: 'emit-version-json',
    closeBundle () {
      const payload = {
        build: buildStamp,
        commit: commitRef
      }
      writeFileSync(
        resolve(__dirname, 'dist/version.json'),
        JSON.stringify(payload, null, 2)
      )
    }
  }
}

// https://vitejs.dev/config/
export default defineConfig({
  plugins: [react(), emitVersionJson()],
  define: {
    'import.meta.env.VITE_APP_BUILD': JSON.stringify(buildStamp),
    'import.meta.env.VITE_APP_COMMIT': JSON.stringify(commitRef)
  },
  resolve: {
    dedupe: ['react', 'react-dom']
  },
  server: {
    port: 5173,
    proxy: {
      '/api': {
        target: 'http://localhost:5000',
        changeOrigin: true
      }
    }
  }
})
