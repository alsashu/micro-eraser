import { createContext, useContext, useState, useEffect, useCallback, ReactNode } from 'react'
import { api, setAuthToken, clearAuthToken } from '../lib/api'
import { logger, EventTypes, generateCorrelationId } from '../lib/logger'

interface User {
  id: string
  email: string
  name: string
  avatarUrl?: string
}

interface AuthContextType {
  user: User | null
  isAuthenticated: boolean
  isLoading: boolean
  login: (email: string, password: string) => Promise<void>
  register: (email: string, name: string, password: string) => Promise<void>
  logout: () => Promise<void>
  refreshAuth: () => Promise<void>
}

const AuthContext = createContext<AuthContextType | undefined>(undefined)

export function AuthProvider({ children }: { children: ReactNode }) {
  const [user, setUser] = useState<User | null>(null)
  const [isLoading, setIsLoading] = useState(true)

  const refreshAuth = useCallback(async () => {
    const refreshToken = localStorage.getItem('refreshToken')
    if (!refreshToken) {
      setIsLoading(false)
      return
    }

    try {
      const response = await api.post('/auth/refresh', { refreshToken })
      const { userId, email, name, accessToken, refreshToken: newRefreshToken } = response.data

      setAuthToken(accessToken)
      localStorage.setItem('refreshToken', newRefreshToken)
      setUser({ id: userId, email, name })
    } catch (error) {
      clearAuthToken()
      localStorage.removeItem('refreshToken')
      setUser(null)
    } finally {
      setIsLoading(false)
    }
  }, [])

  useEffect(() => {
    refreshAuth()
  }, [refreshAuth])

  const login = async (email: string, password: string) => {
    logger.info(EventTypes.LOGIN_ATTEMPT, 'User attempting to login', { email })
    generateCorrelationId() // New correlation ID for new session
    
    try {
      const response = await api.post('/auth/login', { email, password })
      const { userId, email: userEmail, name, accessToken, refreshToken } = response.data

      setAuthToken(accessToken)
      localStorage.setItem('refreshToken', refreshToken)
      setUser({ id: userId, email: userEmail, name })
      
      logger.info(EventTypes.LOGIN_SUCCESS, 'User logged in successfully', { userId, email: userEmail })
    } catch (error) {
      logger.warn(EventTypes.LOGIN_FAILURE, 'Login failed', { email, error: error instanceof Error ? error.message : 'Unknown error' })
      throw error
    }
  }

  const register = async (email: string, name: string, password: string) => {
    logger.info(EventTypes.LOGIN_ATTEMPT, 'User attempting to register', { email, name })
    generateCorrelationId()
    
    try {
      const response = await api.post('/auth/register', { email, name, password })
      const { userId, email: userEmail, name: userName, accessToken, refreshToken } = response.data

      setAuthToken(accessToken)
      localStorage.setItem('refreshToken', refreshToken)
      setUser({ id: userId, email: userEmail, name: userName })
      
      logger.info(EventTypes.LOGIN_SUCCESS, 'User registered successfully', { userId, email: userEmail })
    } catch (error) {
      logger.warn(EventTypes.LOGIN_FAILURE, 'Registration failed', { email, error: error instanceof Error ? error.message : 'Unknown error' })
      throw error
    }
  }

  const logout = async () => {
    logger.info(EventTypes.LOGOUT, 'User logging out', { userId: user?.id })
    
    const refreshToken = localStorage.getItem('refreshToken')
    if (refreshToken) {
      try {
        await api.post('/auth/logout', { refreshToken })
      } catch (error) {
        // Ignore logout errors
      }
    }

    clearAuthToken()
    localStorage.removeItem('refreshToken')
    setUser(null)
    
    // Flush logs before session ends
    await logger.flush()
    generateCorrelationId() // New correlation ID after logout
  }

  return (
    <AuthContext.Provider
      value={{
        user,
        isAuthenticated: !!user,
        isLoading,
        login,
        register,
        logout,
        refreshAuth,
      }}
    >
      {children}
    </AuthContext.Provider>
  )
}

export function useAuth() {
  const context = useContext(AuthContext)
  if (context === undefined) {
    throw new Error('useAuth must be used within an AuthProvider')
  }
  return context
}
