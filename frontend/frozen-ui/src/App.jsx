import React from 'react'
import { Routes, Route, Navigate } from 'react-router-dom'
import { useAuth } from './hooks/useAuth'
import Login from './pages/Login'
import Dashboard from './pages/DashboardTally'
import ProductsPage from './pages/ProductsPage'
import PriceList from './pages/PriceList'
import PurchasesPage from './pages/PurchasesPage'
import PosPage from './pages/PosPage'
import CustomerLedgerPage from './pages/CustomerLedgerPage'
import ExpensesPage from './pages/ExpensesPage'
import ReportsPage from './pages/ReportsPage'
import SalesLedgerPage from './pages/SalesLedgerPage'
import SettingsPage from './pages/SettingsPage'
import UsersPage from './pages/UsersPage'
import BackupPage from './pages/BackupPage'
import Layout from './components/Layout'
import ConnectionStatus from './components/ConnectionStatus'
import ErrorBoundary from './components/ErrorBoundary'

function App() {
  const { user, loading } = useAuth()

  if (loading) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <div className="animate-spin rounded-full h-32 w-32 border-b-2 border-blue-600"></div>
      </div>
    )
  }

  if (!user) {
    return <Login />
  }

  return (
    <ErrorBoundary>
      <ConnectionStatus />
      <Routes>
        <Route path="/" element={<Navigate to="/dashboard" replace />} />
        {/* Dashboard has its own layout */}
        <Route path="/dashboard" element={<Dashboard />} />
        {/* All other pages use Layout with sidebar */}
        <Route element={<Layout />}>
          <Route path="/products" element={<ProductsPage />} />
          <Route path="/pricelist" element={<PriceList />} />
          <Route path="/purchases" element={<PurchasesPage />} />
          <Route path="/pos" element={<PosPage />} />
          <Route path="/ledger" element={<CustomerLedgerPage />} />
          <Route path="/expenses" element={<ExpensesPage />} />
          <Route path="/sales-ledger" element={<SalesLedgerPage />} />
          <Route path="/reports" element={<ReportsPage />} />
          <Route path="/reports/outstanding" element={<ReportsPage />} />
          <Route path="/users" element={<UsersPage />} />
          <Route path="/settings" element={<SettingsPage />} />
          <Route path="/backup" element={<BackupPage />} />
        </Route>
      </Routes>
    </ErrorBoundary>
  )
}

export default App
