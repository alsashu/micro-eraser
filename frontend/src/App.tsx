import { useEffect } from 'react'
import { Routes, Route, Navigate, useLocation } from 'react-router-dom'
import { useAuth } from './contexts/AuthContext'
import LoginPage from './pages/LoginPage'
import RegisterPage from './pages/RegisterPage'
import DashboardPage from './pages/DashboardPage'
import CanvasPage from './pages/CanvasPage'
import InvitePage from './pages/InvitePage'
import { Toaster } from './components/ui/Toaster'
import { logger, EventTypes } from './lib/logger'

function PrivateRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    )
  }

  return isAuthenticated ? <>{children}</> : <Navigate to="/login" />
}

function PublicRoute({ children }: { children: React.ReactNode }) {
  const { isAuthenticated, isLoading } = useAuth()

  if (isLoading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-8 w-8 border-b-2 border-primary"></div>
      </div>
    )
  }

  return !isAuthenticated ? <>{children}</> : <Navigate to="/dashboard" />
}

// Track page views
function usePageTracking() {
  const location = useLocation()
  
  useEffect(() => {
    logger.trackPageView(location.pathname, document.title)
  }, [location.pathname])
}

function App() {
  usePageTracking()
  
  return (
    <>
      <Routes>
        <Route path="/" element={<Navigate to="/dashboard" />} />
        <Route
          path="/login"
          element={
            <PublicRoute>
              <LoginPage />
            </PublicRoute>
          }
        />
        <Route
          path="/register"
          element={
            <PublicRoute>
              <RegisterPage />
            </PublicRoute>
          }
        />
        <Route
          path="/dashboard"
          element={
            <PrivateRoute>
              <DashboardPage />
            </PrivateRoute>
          }
        />
        <Route
          path="/workspace/:workspaceId"
          element={
            <PrivateRoute>
              <DashboardPage />
            </PrivateRoute>
          }
        />
        <Route
          path="/canvas/:canvasId"
          element={
            <PrivateRoute>
              <CanvasPage />
            </PrivateRoute>
          }
        />
        <Route path="/invite/:token" element={<InvitePage />} />
      </Routes>
      <Toaster />
    </>
  )
}

export default App
