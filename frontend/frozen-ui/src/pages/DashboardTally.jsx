import { useState, useEffect, useRef } from 'react'
import { useNavigate, useLocation } from 'react-router-dom'
import { 
  Package, ShoppingCart, Users, Truck, CreditCard, FileText,
  Settings, Database, BarChart3, DollarSign, TrendingUp,
  AlertTriangle, RefreshCw, LogOut, ChevronRight,
  HardDrive, TrendingDown, BookOpen, Wallet
} from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { formatCurrency } from '../utils/currency'
import toast from 'react-hot-toast'
import { reportsAPI } from '../services'
import DesktopSidebar from '../components/DesktopSidebar'
import BottomNav from '../components/BottomNav'
import AlertNotifications from '../components/AlertNotifications'
import { getGatewayMenu } from '../config/navigation'

const DashboardTally = () => {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const location = useLocation()
  const [loading, setLoading] = useState(true)
  const [stats, setStats] = useState({
    salesToday: 0,
    expensesToday: 0,
    profitToday: 0,
    pendingBills: 0,
    pendingBillsAmount: 0,
    purchasesToday: 0,
    paymentsReceivedToday: 0,
    lowStockCount: 0,
    invoicesToday: 0,
    invoicesWeekly: 0,
    invoicesMonthly: 0
  })
  const lastFetchTimeRef = useRef(0)
  const isFetchingRef = useRef(false)
  const fetchTimeoutRef = useRef(null)
  const DASHBOARD_THROTTLE_MS = 10000 // 10 seconds minimum between dashboard requests

  useEffect(() => {
    const fetchStatsThrottled = async () => {
      const now = Date.now()
      const timeSinceLastFetch = now - lastFetchTimeRef.current
      
      if (isFetchingRef.current) {
        return // Already fetching
      }
      
      if (timeSinceLastFetch < DASHBOARD_THROTTLE_MS) {
        // Schedule for later
        if (fetchTimeoutRef.current) {
          clearTimeout(fetchTimeoutRef.current)
        }
        fetchTimeoutRef.current = setTimeout(() => {
          fetchStatsThrottled()
        }, DASHBOARD_THROTTLE_MS - timeSinceLastFetch)
        return
      }
      
      isFetchingRef.current = true
      lastFetchTimeRef.current = now
      
      try {
        await fetchStats()
      } finally {
        isFetchingRef.current = false
      }
    }
    
    // Initial load
    fetchStatsThrottled()
    
    // Declare intervals at the top level
    let interval = null
    
    // Auto-refresh every 2 minutes (increased from 30 seconds)
    interval = setInterval(() => {
      if (document.visibilityState === 'visible' && !isFetchingRef.current) {
        fetchStatsThrottled()
      }
    }, 120000) // 2 minutes
    
    // Listen for global data update events (with debouncing)
    let debounceTimer = null
    const handleDataUpdate = (event) => {
      if (document.visibilityState !== 'visible') return
      const scope = event?.detail?.scope
      if (scope && !['payments', 'customers', 'sales'].includes(scope)) return
      if (debounceTimer) {
        clearTimeout(debounceTimer)
      }
      debounceTimer = setTimeout(() => {
        if (!isFetchingRef.current) {
          fetchStatsThrottled()
        }
      }, 5000) // 5 second debounce
    }
    
    window.addEventListener('dataUpdated', handleDataUpdate)
    window.addEventListener('paymentCreated', handleDataUpdate)
    window.addEventListener('customerCreated', handleDataUpdate)
    
    return () => {
      if (interval) {
        clearInterval(interval)
      }
      if (fetchTimeoutRef.current) {
        clearTimeout(fetchTimeoutRef.current)
      }
      if (debounceTimer) {
        clearTimeout(debounceTimer)
      }
      window.removeEventListener('dataUpdated', handleDataUpdate)
      window.removeEventListener('paymentCreated', handleDataUpdate)
      window.removeEventListener('customerCreated', handleDataUpdate)
    }
  }, [user])

  const fetchStats = async () => {
    try {
      setLoading(true)
      // CRITICAL: Fetch real data for today with explicit date range
      const today = new Date()
      const todayStr = today.toISOString().split('T')[0]
      
      const response = await reportsAPI.getSummaryReport({
        fromDate: todayStr,
        toDate: todayStr
      })
      
      if (response?.success && response?.data) {
        const data = response.data
        if (import.meta.env.DEV) {
          console.log('Dashboard Data Received:', {
            salesToday: data.salesToday,
            expensesToday: data.expensesToday,
            profitToday: data.profitToday,
            pendingBills: data.pendingBills,
            invoicesToday: data.invoicesToday,
            invoicesWeekly: data.invoicesWeekly,
            invoicesMonthly: data.invoicesMonthly
          })
        }
        
        setStats({
          salesToday: parseFloat(data.salesToday || data.SalesToday) || 0,
          expensesToday: parseFloat(data.expensesToday || data.ExpensesToday) || 0,
          profitToday: parseFloat(data.profitToday || data.ProfitToday) || 0,
          pendingBills: parseInt(data.pendingBills || data.PendingBills) || 0,
          pendingBillsAmount: parseFloat(data.pendingBillsAmount || data.PendingBillsAmount) || 0,
          purchasesToday: parseFloat(data.purchasesToday || data.PurchasesToday) || 0,
          paymentsReceivedToday: parseFloat(data.paymentsReceivedToday || data.PaymentsReceivedToday) || 0,
          lowStockCount: Array.isArray(data.lowStockProducts || data.LowStockProducts) ? (data.lowStockProducts || data.LowStockProducts || []).length : 0,
          invoicesToday: parseInt(data.invoicesToday || data.InvoicesToday) || 0,
          invoicesWeekly: parseInt(data.invoicesWeekly || data.InvoicesWeekly) || 0,
          invoicesMonthly: parseInt(data.invoicesMonthly || data.InvoicesMonthly) || 0
        })
      } else {
        console.error('❌ Dashboard API response invalid:', response)
        toast.error('Failed to load dashboard data: Invalid response')
      }
    } catch (error) {
      console.error('❌ Failed to fetch dashboard stats:', error)
      toast.error(`Failed to load dashboard data: ${error.message || 'Unknown error'}`)
    } finally {
      setLoading(false)
    }
  }

  const gatewayMenu = getGatewayMenu(user)

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-slate-50 pb-20 lg:pb-0">
      <DesktopSidebar
        user={user}
        pathname={location.pathname}
        onLogout={logout}
        activeHref="/dashboard"
      />
      <div className="lg:pl-20">
      {/* Top Header Bar - Mobile Responsive */}
      <div className="bg-gradient-to-r from-blue-900 to-blue-800 text-white shadow-lg">
        <div className="flex items-center justify-between px-2 sm:px-3 lg:px-4 py-1.5 sm:py-2 lg:py-3">
          <div className="flex items-center space-x-1.5 sm:space-x-2 flex-1 min-w-0">
            <div className="min-w-0 flex-1">
              <h1 className="text-xs sm:text-sm lg:text-base xl:text-lg font-bold truncate">STARPLUS FOODSTUFF TRADING</h1>
              <p className="text-[9px] sm:text-[10px] text-blue-200 hidden sm:block">Frozen Food Trading & Management System</p>
            </div>
          </div>
          <div className="flex items-center space-x-1 sm:space-x-1.5 flex-shrink-0">
            {/* Top Bar Icons - Admin Only */}
            {user?.role?.toLowerCase() === 'admin' && (
              <>
                <button
                  onClick={() => navigate('/backup')}
                  className="p-2 sm:p-2.5 lg:p-3 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                  title="Backup & Restore"
                >
                  <HardDrive className="h-5 w-5 sm:h-6 sm:w-6 lg:h-7 lg:w-7" />
                </button>
                <button
                  onClick={() => navigate('/settings')}
                  className="p-2 sm:p-2.5 lg:p-3 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                  title="Settings"
                >
                  <Settings className="h-5 w-5 sm:h-6 sm:w-6 lg:h-7 lg:w-7" />
                </button>
                <button
                  onClick={() => navigate('/reports?tab=profit-loss')}
                  className="p-2 sm:p-2.5 lg:p-3 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                  title="Profit & Loss"
                >
                  <TrendingDown className="h-5 w-5 sm:h-6 sm:w-6 lg:h-7 lg:w-7" />
                </button>
                <button
                  onClick={() => navigate('/users')}
                  className="p-2 sm:p-2.5 lg:p-3 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                  title="Users"
                >
                  <Users className="h-5 w-5 sm:h-6 sm:w-6 lg:h-7 lg:w-7" />
                </button>
              </>
            )}
            {/* Alerts Notification Icon */}
            {user?.role?.toLowerCase() === 'admin' && (
              <AlertNotifications />
            )}
            <div className="text-right hidden md:block">
              <p className="text-[10px] sm:text-xs font-medium">{new Date().toLocaleDateString('en-GB', { 
                weekday: 'short', 
                year: 'numeric', 
                month: 'short', 
                day: 'numeric' 
              })}</p>
              <p className="text-[9px] text-amber-200">{user?.name || 'User'}</p>
            </div>
            <button
              onClick={logout}
              className="px-1.5 sm:px-2 lg:px-3 py-1 sm:py-1.5 bg-red-600 hover:bg-red-700 rounded-lg transition flex items-center space-x-1 text-[10px] sm:text-xs cursor-pointer"
            >
              <LogOut className="h-3 w-3 sm:h-3.5 sm:w-3.5" />
              <span className="hidden sm:inline">Logout</span>
            </button>
          </div>
        </div>
      </div>

      {/* Main Layout: Content + Gateway */}
      <div className="flex flex-col lg:flex-row">
        {/* Central Content + Right Gateway Column */}
        <div className="flex-1 flex flex-col lg:flex-row">
          {/* Left: Stats & Quick Actions */}
          <div className="flex-1 p-2 sm:p-3 lg:p-4 space-y-2 sm:space-y-3 lg:space-y-4">
            {/* Stats Cards */}
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-6 gap-1.5 sm:gap-2 lg:gap-3">
              <StatCard
                title="Sales Today"
                value={stats.salesToday}
                icon={DollarSign}
                color="green"
                loading={loading}
              />
              <StatCard
                title="Expenses Today"
                value={stats.expensesToday}
                icon={TrendingUp}
                color="red"
                loading={loading}
              />
              <StatCard
                title="Profit Today"
                value={stats.profitToday}
                icon={TrendingUp}
                color="blue"
                loading={loading}
                adminOnly
              />
              <StatCard
                title="Purchase Today"
                value={stats.purchasesToday}
                icon={Truck}
                color="amber"
                loading={loading}
              />
              <StatCard
                title="Pending Amount"
                value={stats.pendingBillsAmount}
                icon={DollarSign}
                color="yellow"
                loading={loading}
              />
              <StatCard
                title="Received Today"
                value={stats.paymentsReceivedToday}
                icon={DollarSign}
                color="green"
                loading={loading}
              />
            </div>

            {/* Quick Actions Bar */}
            <div className="bg-white rounded-lg shadow-md p-3 sm:p-4 lg:p-6">
              <h2 className="text-base sm:text-lg lg:text-xl font-bold text-gray-900 mb-3 sm:mb-4">Quick Actions</h2>
              <div className="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 lg:gap-5">
                <QuickActionButton
                  icon={ShoppingCart}
                  label="New Invoice"
                  onClick={() => navigate('/pos')}
                  color="blue"
                  shortcut="F3"
                />
                <QuickActionButton
                  icon={Truck}
                  label="New Purchase"
                  onClick={() => navigate('/purchases?action=create')}
                  color="green"
                  shortcut="F4"
                />
                <QuickActionButton
                  icon={FileText}
                  label="Customer Ledger"
                  onClick={() => navigate('/ledger')}
                  color="purple"
                  shortcut="F6"
                />
                {user?.role?.toLowerCase() === 'admin' && (
                  <QuickActionButton
                    icon={Database}
                    label="Backup Now"
                    onClick={() => navigate('/backup')}
                    color="orange"
                    shortcut="Ctrl+B"
                  />
                )}
              </div>
            </div>

            {/* Invoice Counts & Alerts - UPDATED: Removed invoice cards, added Sales Ledger & Expenses */}
            <div className="grid grid-cols-2 sm:grid-cols-2 lg:grid-cols-4 gap-3 sm:gap-4 lg:gap-5">
              <div 
                onClick={() => navigate('/sales-ledger')}
                className="cursor-pointer bg-indigo-50 rounded-lg shadow-md border-2 border-indigo-300 p-4 sm:p-5 lg:p-6 text-center hover:shadow-lg hover:border-indigo-400 transition-all"
              >
                <BookOpen className="h-6 w-6 sm:h-7 sm:w-7 lg:h-8 lg:w-8 mx-auto mb-2 text-indigo-600" />
                <p className="text-xs sm:text-sm font-semibold text-gray-700 mb-1">📘 Sales Ledger</p>
                <p className="text-base sm:text-lg lg:text-xl font-bold text-indigo-700">View</p>
                <p className="text-xs text-indigo-600 mt-1">Click to open →</p>
              </div>
              <div 
                onClick={() => navigate('/expenses')}
                className="cursor-pointer bg-purple-50 rounded-lg shadow-md border-2 border-purple-300 p-4 sm:p-5 lg:p-6 text-center hover:shadow-lg hover:border-purple-400 transition-all"
              >
                <Wallet className="h-6 w-6 sm:h-7 sm:w-7 lg:h-8 lg:w-8 mx-auto mb-2 text-purple-600" />
                <p className="text-xs sm:text-sm font-semibold text-gray-700 mb-1">💰 Expenses</p>
                <p className="text-base sm:text-lg lg:text-xl font-bold text-purple-700">Manage</p>
                <p className="text-xs text-purple-600 mt-1">Click to open →</p>
              </div>
              <AlertCard
                title="💸 Unpaid Bills"
                count={stats.pendingBills}
                icon={AlertTriangle}
                color="yellow"
                onClick={() => navigate('/reports?tab=outstanding')}
              />
              <AlertCard
                title="⚠️ Low Stock"
                count={stats.lowStockCount}
                icon={Package}
                color="red"
                onClick={() => navigate('/products?filter=lowstock')}
              />
            </div>
          </div>

          {/* Right: Gateway Column - Hidden on mobile, shown on tablet+ */}
          <div className="hidden lg:block lg:w-64 xl:w-72 bg-white shadow-2xl border-l border-blue-200">
            <div className="sticky top-0 p-2 sm:p-3 lg:p-4 max-h-screen overflow-y-auto">
              <div className="bg-gradient-to-r from-blue-900 to-blue-800 text-white rounded-lg p-2 sm:p-3 mb-3 sm:mb-4 shadow-lg">
                <h2 className="text-sm sm:text-base font-bold text-center">Gateway of Starplus</h2>
                <p className="text-[10px] text-center text-blue-200 mt-0.5">Foodstuff Trading</p>
              </div>

              <div className="space-y-2 sm:space-y-3">
                {gatewayMenu.map((group, idx) => (
                  <GatewayGroup key={idx} group={group} user={user} navigate={navigate} />
                ))}
              </div>
            </div>
          </div>
        </div>
      </div>
      </div>
      <div className="lg:hidden">
        <BottomNav />
      </div>
    </div>
  )
}

const StatCard = ({ title, value, icon: Icon, color, loading, adminOnly }) => {
  const colorClasses = {
    green: 'bg-green-50 border-green-200 text-green-800',
    red: 'bg-red-50 border-red-200 text-red-800',
    blue: 'bg-blue-50 border-blue-200 text-blue-800',
    amber: 'bg-amber-50 border-amber-200 text-amber-800',
    yellow: 'bg-yellow-50 border-yellow-200 text-yellow-800'
  }
  const iconColorClasses = {
    green: 'text-green-600',
    red: 'text-red-600',
    blue: 'text-blue-600',
    amber: 'text-amber-600',
    yellow: 'text-yellow-600'
  }

  return (
    <div className={`rounded-lg shadow-md border-2 p-1.5 sm:p-2 lg:p-3 ${colorClasses[color] || colorClasses.blue}`}>
      <div className="flex items-center justify-between">
        <div className="min-w-0 flex-1">
          <p className="text-[10px] sm:text-xs font-medium mb-0.5 truncate">{title}</p>
          {loading ? (
            <p className="text-sm sm:text-base lg:text-lg font-bold">...</p>
          ) : (
            <p className="text-sm sm:text-base lg:text-lg font-bold truncate">{formatCurrency(value)}</p>
          )}
        </div>
        <div className={`p-1 sm:p-1.5 lg:p-2 bg-white rounded-lg flex-shrink-0 ${iconColorClasses[color] || iconColorClasses.blue}`}>
          <Icon className="h-3 w-3 sm:h-4 sm:w-4 lg:h-5 lg:w-5" />
        </div>
      </div>
    </div>
  )
}

const QuickActionButton = ({ icon: Icon, label, onClick, color, shortcut }) => {
  const colorClasses = {
    blue: 'bg-blue-100 hover:bg-blue-200 text-blue-900',
    green: 'bg-green-100 hover:bg-green-200 text-green-900',
    purple: 'bg-purple-100 hover:bg-purple-200 text-purple-900',
    orange: 'bg-orange-100 hover:bg-orange-200 text-orange-900'
  }

  return (
    <button
      onClick={onClick}
      className={`${colorClasses[color]} rounded-lg shadow-md border-2 p-4 sm:p-5 lg:p-6 flex flex-col items-center justify-center space-y-3 hover:shadow-lg transition-all group cursor-pointer min-h-[120px]`}
    >
      <div className={`p-2 sm:p-3 bg-white rounded-lg ${colorClasses[color]} shadow-sm`}>
        <Icon className="h-6 w-6 sm:h-7 sm:w-7 lg:h-8 lg:w-8" />
      </div>
      <span className="text-sm sm:text-base font-bold text-center">{label}</span>
      <span className="text-xs opacity-70 group-hover:opacity-100 hidden sm:inline">{shortcut}</span>
    </button>
  )
}

const AlertCard = ({ title, count, icon: Icon, color, onClick }) => {
  const colorClasses = {
    yellow: 'bg-yellow-50 border-yellow-300 text-yellow-900',
    red: 'bg-red-50 border-red-300 text-red-900'
  }

  return (
    <button
      onClick={onClick}
      className={`${colorClasses[color]} rounded-lg shadow-md border-2 p-4 sm:p-5 lg:p-6 w-full text-left hover:shadow-lg transition-all group cursor-pointer`}
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center space-x-3 sm:space-x-4 min-w-0 flex-1">
          <div className={`p-2 sm:p-3 bg-white rounded-lg ${colorClasses[color]} shadow-sm flex-shrink-0`}>
            <Icon className="h-6 w-6 sm:h-7 sm:w-7 lg:h-8 lg:w-8" />
          </div>
          <div className="min-w-0 flex-1">
            <p className="text-sm sm:text-base font-bold truncate">{title}</p>
            <p className="text-2xl sm:text-3xl lg:text-4xl font-bold mt-2">{count}</p>
          </div>
        </div>
        <ChevronRight className="h-5 w-5 sm:h-6 sm:w-6 opacity-0 group-hover:opacity-100 transition-opacity flex-shrink-0" />
      </div>
    </button>
  )
}

const GatewayGroup = ({ group, user, navigate }) => {
  const [expanded, setExpanded] = useState(true)
  const isAdmin = user?.role?.toLowerCase() === 'admin'

  const visibleItems = group.items.filter(item => !item.adminOnly || isAdmin)

  return (
    <div className="border-2 border-blue-200 rounded-lg shadow-md overflow-hidden">
      <button
        onClick={() => setExpanded(!expanded)}
        className="w-full bg-blue-50 hover:bg-blue-100 px-2 sm:px-3 py-1.5 sm:py-2 flex items-center justify-between transition-colors cursor-pointer"
      >
        <h3 className="text-xs sm:text-sm font-bold text-blue-900">{group.title}</h3>
        <ChevronRight className={`h-3 w-3 sm:h-4 sm:w-4 text-blue-700 transform transition-transform ${expanded ? 'rotate-90' : ''}`} />
      </button>
      {expanded && (
        <div className="bg-white divide-y divide-blue-100">
          {visibleItems.map((item, idx) => {
            const Icon = item.icon
            return (
              <button
                key={idx}
                onClick={() => navigate(item.path)}
                className={`w-full px-2 sm:px-3 py-1.5 sm:py-2 flex items-center justify-between hover:bg-blue-50 transition-colors group cursor-pointer ${
                  item.primary ? 'bg-emerald-50 hover:bg-emerald-100' : ''
                }`}
              >
                <div className="flex items-center space-x-1.5 sm:space-x-2 min-w-0 flex-1">
                  <div className={`p-1 sm:p-1.5 rounded-lg flex-shrink-0 ${
                    item.primary ? 'bg-emerald-200' : 'bg-blue-100'
                  } group-hover:shadow-md transition-shadow`}>
                    <Icon className="h-3 w-3 sm:h-4 sm:w-4" />
                  </div>
                  <div className="text-left min-w-0 flex-1">
                    <p className="text-[10px] sm:text-xs font-medium text-gray-900 truncate">{item.label}</p>
                    <p className="text-[9px] text-gray-500 hidden sm:block">{item.shortcut}</p>
                  </div>
                </div>
                <ChevronRight className="h-2.5 w-2.5 sm:h-3 sm:w-3 text-gray-400 group-hover:text-gray-600 flex-shrink-0" />
              </button>
            )
          })}
        </div>
      )}
    </div>
  )
}

export default DashboardTally

