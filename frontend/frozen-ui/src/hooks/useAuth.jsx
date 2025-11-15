import { createContext, useContext, useState, useEffect } from 'react'
import { authAPI } from '../services'

const AuthContext = createContext()

export const useAuth = () => {
  const context = useContext(AuthContext)
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}

export const AuthProvider = ({ children }) => {
  const [user, setUser] = useState(null)
  const [loading, setLoading] = useState(true)

  useEffect(() => {
    const token = localStorage.getItem('token')
    const userData = localStorage.getItem('user')
    
    if (token && userData) {
      try {
        const parsedUser = JSON.parse(userData)
        setUser(parsedUser)
        
        // Validate token silently - don't show errors on initial load
        authAPI.validateToken()
          .then(response => {
            if (response?.success && response?.data) {
              // Update user data if response contains user info
              const updatedUser = {
                id: response.data.UserId || parsedUser.id,
                role: response.data.Role || parsedUser.role,
                name: response.data.Name || parsedUser.name,
                companyName: parsedUser.companyName
              }
              setUser(updatedUser)
              localStorage.setItem('user', JSON.stringify(updatedUser))
            } else {
              // Token invalid - logout silently
              logout()
            }
          })
          .catch((error) => {
            // Only logout if it's an authentication error, not network errors
            if (error.response?.status === 401) {
              logout()
            }
            // For network errors, keep the user logged in with cached data
          })
          .finally(() => {
            setLoading(false)
          })
      } catch (error) {
        // Invalid user data in localStorage - clear it
        logout()
        setLoading(false)
      }
    } else {
      setLoading(false)
    }
  }, [])

  const login = async (credentials) => {
    try {
      const response = await authAPI.login(credentials)
      if (response.success) {
        const userData = {
          id: response.data.userId,
          role: response.data.role || 'Staff',
          name: response.data.name || response.data.Name || 'User',
          companyName: response.data.companyName
        }
        
        localStorage.setItem('token', response.data.token)
        localStorage.setItem('user', JSON.stringify(userData))
        setUser(userData)
        
        return { success: true, data: response.data }
      } else {
        return { success: false, message: response.message }
      }
    } catch (error) {
      return { 
        success: false, 
        message: error.response?.data?.message || 'Login failed' 
      }
    }
  }

  const logout = () => {
    localStorage.removeItem('token')
    localStorage.removeItem('user')
    setUser(null)
  }

  const value = {
    user,
    login,
    logout,
    loading
  }

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  )
}
