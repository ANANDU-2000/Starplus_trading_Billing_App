import React from 'react'
import ReactDOM from 'react-dom/client'
import { BrowserRouter } from 'react-router-dom'
import { AuthProvider } from './hooks/useAuth'
import { Toaster } from 'react-hot-toast'
import App from './App.jsx'
import './index.css'

// Force favicon update on load (for cache-busting)
const updateFavicon = () => {
  const timestamp = Date.now()
  const favicon = document.querySelector("link[rel='icon']") || document.querySelector("link[rel='shortcut icon']")
  if (favicon) {
    const newHref = favicon.href.split('?')[0] + '?v=' + timestamp
    favicon.href = newHref
  }
  
  // Update apple touch icon
  const appleIcon = document.querySelector("link[rel='apple-touch-icon']")
  if (appleIcon) {
    const newHref = appleIcon.href.split('?')[0] + '?v=' + timestamp
    appleIcon.href = newHref
  }
  
  // Update manifest
  const manifest = document.querySelector("link[rel='manifest']")
  if (manifest) {
    const newHref = manifest.href.split('?')[0] + '?v=' + timestamp
    manifest.href = newHref
  }
}

// Update favicon on mount
updateFavicon()

ReactDOM.createRoot(document.getElementById('root')).render(
  <React.StrictMode>
    <BrowserRouter>
      <AuthProvider>
        <App />
      </AuthProvider>
      <Toaster 
        position="top-right"
        toastOptions={{
          duration: 4000,
          style: {
            background: '#363636',
            color: '#fff',
          },
        }}
      />
    </BrowserRouter>
  </React.StrictMode>,
)
