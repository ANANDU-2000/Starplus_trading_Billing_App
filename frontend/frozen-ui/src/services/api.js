import axios from 'axios'
import toast from 'react-hot-toast'
import { connectionManager } from './connectionManager'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001/api'

// Error throttling to prevent flooding
let lastErrorToast = null
let errorToastCount = 0
let lastNetworkErrorToast = null
const ERROR_THROTTLE_MS = 3000 // Show max 1 error toast per 3 seconds for general errors
const NETWORK_ERROR_THROTTLE_MS = 15000 // Show max 1 network error toast per 15 seconds

// In-flight GET dedup: identical GET while pending returns the same promise
const inFlightGetRequests = new Map()

// Generate request key for deduplication
const getRequestKey = (config) => {
  if (!config) {
    return `UNKNOWN_${Date.now()}_${Math.random()}`
  }
  const method = (config.method || 'GET').toUpperCase()
  const url = config.url || ''
  const params = config.params || {}
  return `${method}_${url}_${JSON.stringify(params)}`
}

const showThrottledError = (message, isNetworkError = false) => {
  const now = Date.now()
  const throttleTime = isNetworkError ? NETWORK_ERROR_THROTTLE_MS : ERROR_THROTTLE_MS
  const lastToast = isNetworkError ? lastNetworkErrorToast : lastErrorToast
  
  if (lastToast && (now - lastToast) < throttleTime) {
    errorToastCount++
    return // Skip this error, already showing one
  }
  
  if (isNetworkError) {
    lastNetworkErrorToast = now
  } else {
  lastErrorToast = now
  }
  
  errorToastCount = 1
  
  // For network errors, show longer duration and less intrusive style
  if (isNetworkError) {
    toast.error(message, { 
      duration: 6000,
      id: 'network-error', // Use same ID to replace previous toast
      position: 'top-center'
    })
  } else {
  toast.error(message, { duration: 4000 })
  }
}

const api = axios.create({
  baseURL: API_BASE_URL,
  headers: {
    'Content-Type': 'application/json',
  },
})

// Request interceptor to add auth token, check connection, and throttle requests
api.interceptors.request.use(
  (config) => {
    // CRITICAL: Ensure config exists and has required properties
    if (!config) {
      config = {}
    }
    if (!config.method) {
      config.method = 'GET'
    }
    if (!config.url) {
      config.url = ''
    }
    
    // Check if we should allow this request
    if (!connectionManager.shouldAllowRequest()) {
      const error = new Error('Server connection unavailable. Please wait...')
      error.config = config
      error.isConnectionBlocked = true
      return Promise.reject(error)
    }

    const requestKey = getRequestKey(config)
    config._requestKey = requestKey

    // Add auth token
    const token = localStorage.getItem('token')
    if (token) {
      config.headers = config.headers || {}
      config.headers.Authorization = `Bearer ${token}`
    }
    
    // Add retry configuration
    config._retryCount = config._retryCount || 0
    config._maxRetries = 3

    return config
  },
  (error) => {
    return Promise.reject(error)
  }
)

// Response interceptor to handle errors and cleanup
// Offline detection and error handling
api.interceptors.response.use(
  (response) => {
    connectionManager.markConnected()
    return response
  },
  async (error) => {
    // Handle connection blocked errors
    if (error.isConnectionBlocked) {
      return Promise.reject(error)
    }

    // Handle network/connection errors
    const isNetworkError = !error.response && (
      error.message === 'Network Error' || 
      error.code === 'ERR_NETWORK' || 
      error.code === 'ERR_CORS' ||
      error.code === 'ECONNREFUSED' ||
      error.code === 'ETIMEDOUT' ||
      error.message?.includes('Failed to fetch') ||
      error.message?.includes('NetworkError')
    )

    if (isNetworkError) {
      // Mark connection as failed
      connectionManager.markDisconnected()
      
      // Show throttled error message
      const errorMsg = error.code === 'ERR_CORS' 
        ? 'Backend server may have stopped. Please restart the backend.'
        : `Cannot connect to server. Please ensure the backend is running at ${API_BASE_URL.replace('/api', '')}`
      
      showThrottledError(errorMsg, true)
      
      // Only log once per error type
      if (errorToastCount === 1) {
        console.error('Network Error Details:', {
          url: error.config?.url,
          baseURL: API_BASE_URL,
          method: error.config?.method,
          message: error.message,
          code: error.code
        })
      }
      
      return Promise.reject(error)
    }

    // Clean up in-flight GET dedup entry on error
    if (error.config?._requestKey) {
      inFlightGetRequests.delete(error.config._requestKey)
    }

    // Handle 429 Too Many Requests
    if (error.response?.status === 429) {
      connectionManager.markConnected() // Server is responding
      
      const retryAfter = error.response?.headers?.['retry-after'] || 5
      const message = `Too many requests. Please wait ${retryAfter} seconds before trying again.`
      
      showThrottledError(message, false)
      
      // Don't log every 429 error to prevent console flooding
      if (errorToastCount === 1) {
        console.warn('⚠️ Rate limit exceeded (429). Requests are being throttled.')
      }
      
      return Promise.reject(error)
    }
    
    // Handle throttled requests (from interceptor) - silently reject
    if (error.isThrottled) {
      // Don't show error for throttled requests - they're expected behavior
      return Promise.reject(error)
    }
    
    // Handle rate limited requests
    if (error.isRateLimited) {
      showThrottledError('Too many requests in progress. Please wait...', false)
      return Promise.reject(error)
    }

    // Handle 401 Unauthorized errors
    if (error.response?.status === 401) {
      connectionManager.markConnected() // Server is responding, just auth issue
      
      const authFailure = error.response?.headers?.['x-auth-failure']
      const errorMessage = error.response?.data?.message || ''
      const tokenExpired = error.response?.headers?.['token-expired'] === 'true'
      
      // Check various indicators that this is an authentication failure (need to login)
      const isAuthFailure = authFailure || 
                            tokenExpired ||
                            errorMessage.toLowerCase().includes('session') ||
                            errorMessage.toLowerCase().includes('expired') ||
                            errorMessage.toLowerCase().includes('token') ||
                            errorMessage.toLowerCase().includes('authentication') ||
                            errorMessage.toLowerCase().includes('login')
      
      if (isAuthFailure) {
        // Clear auth data
        localStorage.removeItem('token')
        localStorage.removeItem('user')
        
        // Show appropriate error message (only once)
        const message = errorMessage || 
                       (tokenExpired ? 'Your session has expired. Please login again.' : 
                        'Authentication required. Please login again.')
        
        toast.error(message, { duration: 3000 })
        
        // Small delay to let the toast show before redirect
        setTimeout(() => {
          window.location.href = '/login'
        }, 1500)
      } else {
        // It's likely a permissions issue (authorization), not authentication
        // Don't logout, just show error
        showThrottledError(errorMessage || 'You are not authorized to perform this action')
      }
    } else if (error.response?.status >= 500) {
      // Server errors - server is responding but has issues
      connectionManager.markConnected()
      showThrottledError(error.response?.data?.message || 'Server error. Please try again later.')
    } else if (error.response?.data?.message) {
      // Server is responding with message
      connectionManager.markConnected()
      showThrottledError(error.response.data.message)
    } else if (error.response?.data?.errors && Array.isArray(error.response.data.errors)) {
      // Server is responding with errors array
      connectionManager.markConnected()
      const errorMsg = error.response.data.errors.join(', ')
      showThrottledError(errorMsg)
    } else if (error.response?.data) {
      // Server is responding but structure is different
      connectionManager.markConnected()
      showThrottledError('An error occurred. Please try again.')
    } else {
      // Unknown error
      connectionManager.markConnected()
      showThrottledError('An error occurred. Please try again.')
    }
    
    return Promise.reject(error)
  }
)

const originalRequest = api.request.bind(api)
api.request = (config) => {
  const normalized = typeof config === 'string'
    ? { url: config, method: 'get' }
    : { ...config }
  const method = (normalized.method || 'GET').toUpperCase()
  const requestKey = getRequestKey(normalized)
  normalized._requestKey = requestKey

  const skipDedup = normalized._skipDedup || normalized._isRetry || method !== 'GET'

  if (!skipDedup && inFlightGetRequests.has(requestKey)) {
    return inFlightGetRequests.get(requestKey)
  }

  const promise = originalRequest(normalized).finally(() => {
    if (!skipDedup) {
      inFlightGetRequests.delete(requestKey)
    }
  })

  if (!skipDedup) {
    inFlightGetRequests.set(requestKey, promise)
  }

  return promise
}

export default api
