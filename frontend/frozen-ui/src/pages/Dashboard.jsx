import { useState, useEffect, useMemo } from 'react'
import { useNavigate } from 'react-router-dom'
import { 
  TrendingUp, 
  TrendingDown, 
  Package, 
  DollarSign, 
  AlertTriangle,
  Plus,
  Download,
  RefreshCw,
  Eye,
  FileText,
  UserPlus,
  Receipt,
  Database,
  CheckCircle,
  XCircle,
  ShoppingCart,
  CreditCard,
  Users,
  BarChart3,
  Settings,
  LogOut,
  Activity,
  BookOpen,
  Wallet
} from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { formatCurrency } from '../utils/currency'
import { showToast } from '../utils/toast'
import { LoadingCard } from '../components/Loading'
import { StatCard } from '../components/ui'
import { reportsAPI, adminAPI, productsAPI } from '../services'
import PendingBillsPanel from '../components/PendingBillsPanel'
import QuickActionsPanel from '../components/QuickActionsPanel'
import { 
  LineChart, 
  Line, 
  XAxis, 
  YAxis, 
  CartesianGrid, 
  Tooltip, 
  ResponsiveContainer,
  BarChart,
  Bar,
  Area,
  AreaChart
} from 'recharts'
import toast from 'react-hot-toast'

const Dashboard = () => {
  const { user, logout } = useAuth()
  const navigate = useNavigate()
  const [loading, setLoading] = useState(true)
  const [summary, setSummary] = useState({
    salesToday: 0,
    purchasesToday: 0,
    expensesToday: 0,
    profitToday: 0,
    pendingBillsCount: 0,
    pendingBillsAmount: 0,
    paidBillsCount: 0,
    paidBillsAmount: 0,
    salesChange: 0,
    purchasesChange: 0,
    expensesChange: 0,
    profitChange: 0
  })
  const [salesData, setSalesData] = useState([])
  const [lowStockProducts, setLowStockProducts] = useState([])
  const [pendingBills, setPendingBills] = useState([])
  const [aiSuggestions, setAiSuggestions] = useState(null)
  const [pendingFilter, setPendingFilter] = useState('all')
  const [pendingSearch, setPendingSearch] = useState('')
  const [dbStatus, setDbStatus] = useState(true)
  const [lastBackup, setLastBackup] = useState(null)
  const [backupLoading, setBackupLoading] = useState(false)

  const fetchDashboardData = async () => {
    try {
      setLoading(true)
      
      console.log('🔄 Fetching dashboard data...')
      
      // Get date range - use last 30 days to ensure we get data
      const today = new Date()
      const thirtyDaysAgo = new Date(today)
      thirtyDaysAgo.setDate(thirtyDaysAgo.getDate() - 30)
      
      // For summary, use today only
      const todayFrom = new Date(today.getFullYear(), today.getMonth(), today.getDate()).toISOString().split('T')[0]
      const todayTo = todayFrom
      
      // For sales chart, use last 7 days
      const salesFrom = new Date(today)
      salesFrom.setDate(salesFrom.getDate() - 7)
      const salesFromDate = salesFrom.toISOString().split('T')[0]
      const salesToDate = today.toISOString().split('T')[0]
      
      console.log('📅 Date ranges:', { 
        summaryFrom: todayFrom, 
        summaryTo: todayTo,
        salesFrom: salesFromDate,
        salesTo: salesToDate
      })
      
      const summaryResponse = await reportsAPI.getSummaryReport({ 
        fromDate: todayFrom,
        toDate: todayTo
      })
      
      console.log('📊 Summary response:', summaryResponse)
      
      if (summaryResponse?.success) {
        const data = summaryResponse.data || {}
        console.log('✅ Summary data received:', {
          salesToday: data.salesToday,
          purchasesToday: data.purchasesToday,
          expensesToday: data.expensesToday,
          profitToday: data.profitToday,
          pendingBills: data.pendingBills,
          lowStockCount: data.lowStockProducts?.length
        })
        
        setSummary({
          salesToday: data.salesToday || 0,
          purchasesToday: data.purchasesToday || 0,
          expensesToday: data.expensesToday || 0,
          profitToday: data.profitToday || 0,
          salesChange: 12,
          purchasesChange: 8,
          expensesChange: -5,
          profitChange: 15
        })
        setLowStockProducts(data.lowStockProducts || [])
      } else {
        console.error('❌ Summary response not successful:', summaryResponse)
        toast.error(summaryResponse?.message || 'Failed to load summary data')
      }

      // CRITICAL: Get ALL pending bills (no date filtering)
      // This ensures backdated invoices show as overdue
      const pendingResponse = await reportsAPI.getPendingBills({ 
        status: pendingFilter === 'all' ? null : pendingFilter,
        search: pendingSearch || null
      })
      console.log('📋 Pending bills response:', pendingResponse)
      if (pendingResponse?.success) {
        const pendingData = pendingResponse.data || []
        console.log(`✅ Loaded ${pendingData.length} pending bills`)
        setPendingBills(pendingData)
      } else {
        console.error('❌ Pending bills response not successful:', pendingResponse)
      }

      try {
        const aiResponse = await reportsAPI.getAISuggestions({ periodDays: 30 })
        if (aiResponse.success) {
          setAiSuggestions(aiResponse.data)
        }
      } catch (error) {
        console.warn('AI suggestions failed:', error)
        setAiSuggestions(null)
      }

      console.log('📈 Fetching sales report:', { salesFrom: salesFromDate, salesTo: salesToDate })
      
      const salesResponse = await reportsAPI.getSalesReport({
        fromDate: salesFromDate,
        toDate: salesToDate,
        pageSize: 100
      })
      
      console.log('📈 Sales report response:', salesResponse)
      
      if (salesResponse?.success && salesResponse.data?.items) {
        const items = salesResponse.data.items || []
        console.log(`✅ Loaded ${items.length} sales records`)
        const grouped = items.reduce((acc, sale) => {
          const date = sale.invoiceDate?.split('T')[0] || new Date(sale.invoiceDate).toISOString().split('T')[0]
          if (!acc[date]) {
            acc[date] = { date, sales: 0, purchases: 0 }
          }
          acc[date].sales += sale.grandTotal || 0
          return acc
        }, {})
        const salesDataArray = Object.values(grouped).sort((a, b) => a.date.localeCompare(b.date))
        console.log('📊 Grouped sales data:', salesDataArray)
        setSalesData(salesDataArray)
      } else {
        console.warn('⚠️ Sales report not successful or no items:', salesResponse)
        setSalesData([])
      }

      if (user?.role?.toLowerCase() === 'admin') {
        try {
          const backupsResponse = await adminAPI.getBackups()
          if (backupsResponse.success && backupsResponse.data?.length > 0) {
            setLastBackup(backupsResponse.data[0])
          }
        } catch (error) {
          console.error('Failed to fetch backup info:', error)
        }
      }

      setDbStatus(true)
    } catch (error) {
      console.error('Dashboard data fetch error:', error)
      toast.error('Failed to load dashboard data')
      setDbStatus(false)
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    fetchDashboardData()
    // Auto-refresh dashboard every 60 seconds (reduced frequency)
    // Only refresh if page is visible
    const interval = setInterval(() => {
      if (document.visibilityState === 'visible') {
        fetchDashboardData()
      }
    }, 60000) // 60 seconds - reduced from 30
    return () => clearInterval(interval)
  }, [pendingFilter, pendingSearch])

  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.ctrlKey || e.metaKey) {
        if (e.key === 'b') {
          e.preventDefault()
          navigate('/pos')
        } else if (e.key === 'u') {
          e.preventDefault()
          navigate('/purchases')
        } else if (e.key === 'c') {
          e.preventDefault()
          navigate('/ledger')
        } else if (e.key === 'l') {
          e.preventDefault()
          navigate('/reports')
        } else if (e.key === 'k' && user?.role?.toLowerCase() === 'admin') {
          e.preventDefault()
          handleBackup()
        }
      }
    }
    window.addEventListener('keydown', handleKeyDown)
    return () => window.removeEventListener('keydown', handleKeyDown)
  }, [user, navigate])

  const handleBackup = async () => {
    if (user?.role?.toLowerCase() !== 'admin') return
    
    setBackupLoading(true)
    try {
      const response = await adminAPI.createBackup()
      if (response.success) {
        toast.success('Backup created successfully!')
        fetchDashboardData()
      } else {
        toast.error(response.message || 'Failed to create backup')
      }
    } catch (error) {
      toast.error('Failed to create backup')
    } finally {
      setBackupLoading(false)
    }
  }

  const filteredPendingBills = useMemo(() => {
    if (!pendingSearch) return pendingBills
    return pendingBills.filter(bill => 
      bill.invoiceNo?.toLowerCase().includes(pendingSearch.toLowerCase()) ||
      bill.customerName?.toLowerCase().includes(pendingSearch.toLowerCase())
    )
  }, [pendingBills, pendingSearch])

  const handleBillClick = (bill) => {
    navigate(`/sales/${bill.id}`)
  }

  if (loading) {
    return <LoadingCard message="Loading dashboard..." />
  }

  if (!summary) {
    return <LoadingCard message="Loading dashboard data..." />
  }

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Modern Header - Mobile Responsive */}
      <div className="bg-white border-b border-gray-200 shadow-sm">
        <div className="max-w-7xl mx-auto px-2 sm:px-4 lg:px-8">
          <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between py-2 sm:py-0 sm:h-16 gap-2 sm:gap-0">
            <div className="flex items-center">
              <BarChart3 className="h-6 w-6 sm:h-8 sm:w-8 text-blue-600 mr-2 sm:mr-3" />
              <div>
                <h1 className="text-lg sm:text-2xl font-bold text-gray-900">Dashboard</h1>
                <p className="text-xs sm:text-sm text-gray-600 hidden sm:block">Welcome back, {user?.name}</p>
              </div>
            </div>
            <div className="flex items-center gap-2 w-full sm:w-auto">
              <button
                onClick={fetchDashboardData}
                className="flex-1 sm:flex-none inline-flex items-center justify-center px-3 sm:px-4 py-1.5 sm:py-2 border border-gray-300 rounded-lg shadow-sm text-xs sm:text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 transition-colors"
              >
                <RefreshCw className="h-3.5 w-3.5 sm:h-4 sm:w-4 sm:mr-2" />
                <span className="hidden sm:inline">Refresh</span>
              </button>
              {user?.role?.toLowerCase() === 'admin' && (
                <button
                  onClick={handleBackup}
                  disabled={backupLoading}
                  className="flex-1 sm:flex-none inline-flex items-center justify-center px-3 sm:px-4 py-1.5 sm:py-2 border border-transparent rounded-lg shadow-sm text-xs sm:text-sm font-medium text-white bg-blue-600 hover:bg-blue-700 disabled:opacity-50 transition-colors"
                >
                  <Database className="h-3.5 w-3.5 sm:h-4 sm:w-4 sm:mr-2" />
                  <span className="hidden sm:inline">{backupLoading ? 'Backing up...' : 'Backup'}</span>
                  <span className="sm:hidden">{backupLoading ? '...' : 'Backup'}</span>
                </button>
              )}
            </div>
          </div>
        </div>
      </div>

      {/* Main Content - Mobile Responsive */}
      <div className="max-w-7xl mx-auto px-2 sm:px-4 lg:px-8 py-2 sm:py-4 lg:py-8">
        {/* KPI Cards - Compact on Mobile */}
        <div className="grid grid-cols-2 sm:grid-cols-3 md:grid-cols-6 gap-2 sm:gap-3 lg:gap-4 mb-3 sm:mb-6 lg:mb-8">
          <StatCard
            title="Sales Today"
            value={summary.salesToday}
            change={summary.salesChange}
            changeType="positive"
            icon={DollarSign}
            iconColor="green"
          />
          <StatCard
            title="Expenses Today"
            value={summary.expensesToday}
            change={summary.expensesChange}
            changeType="negative"
            icon={TrendingDown}
            iconColor="red"
          />
          <StatCard
            title="Purchases Today"
            value={summary.purchasesToday}
            change={summary.purchasesChange}
            changeType="positive"
            icon={ShoppingCart}
            iconColor="orange"
          />
          <StatCard
            title="Profit Today"
            value={summary.profitToday}
            change={summary.profitChange}
            changeType={summary.profitToday >= 0 ? "positive" : "negative"}
            icon={TrendingUp}
            iconColor="blue"
          />
          <div 
            onClick={() => navigate('/sales-ledger')}
            className="cursor-pointer bg-white rounded-lg shadow-sm border border-gray-200 p-3 sm:p-4 hover:shadow-md hover:border-blue-300 transition-all"
          >
            <div className="flex items-center justify-between mb-2">
              <div className="p-1.5 sm:p-2 bg-indigo-100 rounded-lg">
                <BookOpen className="h-4 w-4 sm:h-5 sm:w-5 text-indigo-600" />
              </div>
            </div>
            <h3 className="text-xs sm:text-sm font-medium text-gray-600 mb-0.5 sm:mb-1">Sales Ledger</h3>
            <p className="text-lg sm:text-2xl font-bold text-gray-900">View</p>
            <p className="text-xs text-indigo-600 mt-1 font-medium">Click to open →</p>
          </div>
          <div 
            onClick={() => navigate('/expenses')}
            className="cursor-pointer bg-white rounded-lg shadow-sm border border-gray-200 p-3 sm:p-4 hover:shadow-md hover:border-purple-300 transition-all"
          >
            <div className="flex items-center justify-between mb-2">
              <div className="p-1.5 sm:p-2 bg-purple-100 rounded-lg">
                <Wallet className="h-4 w-4 sm:h-5 sm:w-5 text-purple-600" />
              </div>
            </div>
            <h3 className="text-xs sm:text-sm font-medium text-gray-600 mb-0.5 sm:mb-1">Expenses</h3>
            <p className="text-lg sm:text-2xl font-bold text-gray-900">Manage</p>
            <p className="text-xs text-purple-600 mt-1 font-medium">Click to open →</p>
          </div>
        </div>

        {/* Charts Section */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6 mb-8">
          {/* Sales Trend Chart */}
          <div className="lg:col-span-2 bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">Sales Trend</h2>
              <button className="text-sm text-blue-600 hover:text-blue-700 font-medium">
                View Report
              </button>
            </div>
            <ResponsiveContainer width="100%" height={300}>
              <AreaChart data={salesData}>
                <defs>
                  <linearGradient id="colorSales" x1="0" y1="0" x2="0" y2="1">
                    <stop offset="5%" stopColor="#10B981" stopOpacity={0.3}/>
                    <stop offset="95%" stopColor="#10B981" stopOpacity={0}/>
                  </linearGradient>
                </defs>
                <CartesianGrid strokeDasharray="3 3" className="opacity-30" />
                <XAxis 
                  dataKey="date" 
                  tick={{ fontSize: 12 }}
                  tickFormatter={(value) => new Date(value).toLocaleDateString('en-US', { month: 'short', day: 'numeric' })}
                />
                <YAxis tick={{ fontSize: 12 }} />
                <Tooltip 
                  formatter={(value) => formatCurrency(value)}
                  labelStyle={{ color: '#374151' }}
                />
                <Area 
                  type="monotone" 
                  dataKey="sales" 
                  stroke="#10B981" 
                  strokeWidth={2}
                  fill="url(#colorSales)"
                />
              </AreaChart>
            </ResponsiveContainer>
          </div>

          {/* Quick Stats */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Quick Stats</h2>
            <div className="space-y-4">
              <div className="flex items-center justify-between p-3 bg-blue-50 rounded-lg">
                <div className="flex items-center">
                  <AlertTriangle className="h-5 w-5 text-orange-600 mr-3" />
                  <div>
                    <p className="text-sm font-medium text-gray-900">💰 Unpaid Bills</p>
                    <p className="text-xs text-gray-600">
                      {summary?.pendingBillsCount || pendingBills.length} customers owe you money
                    </p>
                  </div>
                </div>
                <div className="text-right">
                  <span className="text-2xl font-bold text-orange-600">{summary?.pendingBillsCount || pendingBills.length}</span>
                  <p className="text-xs text-orange-600">{formatCurrency(summary?.pendingBillsAmount || 0)}</p>
                </div>
              </div>
              
              <div className="flex items-center justify-between p-3 bg-green-50 rounded-lg">
                <div className="flex items-center">
                  <CheckCircle className="h-5 w-5 text-green-600 mr-3" />
                  <div>
                    <p className="text-sm font-medium text-gray-900">✅ Paid Bills</p>
                    <p className="text-xs text-gray-600">
                      {summary?.paidBillsCount || 0} payments received
                    </p>
                  </div>
                </div>
                <div className="text-right">
                  <span className="text-2xl font-bold text-green-600">{summary?.paidBillsCount || 0}</span>
                  <p className="text-xs text-green-600">{formatCurrency(summary?.paidBillsAmount || 0)}</p>
                </div>
              </div>
              
              <div className="flex items-center justify-between p-3 bg-yellow-50 rounded-lg">
                <div className="flex items-center">
                  <Package className="h-5 w-5 text-yellow-600 mr-3" />
                  <div>
                    <p className="text-sm font-medium text-gray-900">⚠️ Low Stock Items</p>
                    <p className="text-xs text-gray-600">{lowStockProducts.length} products need restocking</p>
                  </div>
                </div>
                <span className="text-2xl font-bold text-yellow-600">{lowStockProducts.length}</span>
              </div>

              <div className="flex items-center justify-between p-3 bg-green-50 rounded-lg">
                <div className="flex items-center">
                  <Activity className="h-5 w-5 text-green-600 mr-3" />
                  <div>
                    <p className="text-sm font-medium text-gray-900">System Status</p>
                    <p className="text-xs text-gray-600">All systems operational</p>
                  </div>
                </div>
                <CheckCircle className="h-6 w-6 text-green-600" />
              </div>

              {lastBackup && (
                <div className="flex items-center justify-between p-3 bg-gray-50 rounded-lg">
                  <div>
                    <p className="text-sm font-medium text-gray-900">Last Backup</p>
                    <p className="text-xs text-gray-600">{new Date(lastBackup.createdAt).toLocaleDateString()}</p>
                  </div>
                  <Database className="h-5 w-5 text-gray-600" />
                </div>
              )}
            </div>
          </div>
        </div>

        {/* Alerts & Actions */}
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-6 mb-8">
          {/* Low Stock Alert */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">⚠️ Stock Running Low</h2>
              <button 
                onClick={() => navigate('/products')}
                className="text-sm text-blue-600 hover:text-blue-700 font-medium"
              >
                Add Stock
              </button>
            </div>
            <div className="space-y-2">
              {lowStockProducts.slice(0, 5).map((product) => (
                <div key={product.id} className="flex items-center justify-between p-3 bg-yellow-50 rounded-lg hover:bg-yellow-100 transition-colors">
                  <div className="flex-1">
                    <p className="text-sm font-medium text-gray-900">{product.nameEn}</p>
                    <p className="text-xs text-red-600 font-medium">⚠️ Only {product.stockQty} {product.unitType} left - Order more soon!</p>
                  </div>
                  <div className="text-right">
                    <p className="text-sm font-bold text-red-600">{product.stockQty} {product.unitType}</p>
                    <p className="text-xs text-gray-500">Min need: {product.reorderLevel}</p>
                  </div>
                </div>
              ))}
              {lowStockProducts.length === 0 && (
                <div className="text-center py-6">
                  <CheckCircle className="h-12 w-12 text-green-500 mx-auto mb-2" />
                  <p className="text-green-600 font-medium">✅ All products have enough stock</p>
                </div>
              )}
            </div>
          </div>

          {/* Quick Actions */}
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <h2 className="text-lg font-semibold text-gray-900 mb-4">Quick Actions</h2>
            <div className="grid grid-cols-2 gap-3">
              <button
                onClick={() => navigate('/pos')}
                className="flex items-center justify-center p-4 bg-blue-50 hover:bg-blue-100 rounded-lg transition-colors"
              >
                <ShoppingCart className="h-6 w-6 text-blue-600 mr-2" />
                <span className="text-sm font-medium text-blue-900">New Invoice</span>
              </button>
              
              <button
                onClick={() => navigate('/products')}
                className="flex items-center justify-center p-4 bg-green-50 hover:bg-green-100 rounded-lg transition-colors"
              >
                <Plus className="h-6 w-6 text-green-600 mr-2" />
                <span className="text-sm font-medium text-green-900">Add Product</span>
              </button>

              <button
                onClick={() => navigate('/customers')}
                className="flex items-center justify-center p-4 bg-purple-50 hover:bg-purple-100 rounded-lg transition-colors"
              >
                <UserPlus className="h-6 w-6 text-purple-600 mr-2" />
                <span className="text-sm font-medium text-purple-900">Add Customer</span>
              </button>

              <button
                onClick={() => navigate('/reports')}
                className="flex items-center justify-center p-4 bg-orange-50 hover:bg-orange-100 rounded-lg transition-colors"
              >
                <FileText className="h-6 w-6 text-orange-600 mr-2" />
                <span className="text-sm font-medium text-orange-900">View Reports</span>
              </button>
            </div>
          </div>
        </div>

        {/* Pending Bills */}
        {pendingBills.length > 0 && (
          <div className="bg-white rounded-xl shadow-sm border border-gray-200 p-6">
            <div className="flex items-center justify-between mb-4">
              <h2 className="text-lg font-semibold text-gray-900">💰 Customers Who Owe You Money</h2>
              <button 
                onClick={() => navigate('/payments')}
                className="text-sm text-blue-600 hover:text-blue-700 font-medium"
              >
                Collect Payments
              </button>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full divide-y divide-gray-200">
                <thead className="bg-gray-50">
                  <tr>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Invoice #</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Customer</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Bill Date</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Amount Due</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Days Pending</th>
                    <th className="px-6 py-3 text-left text-xs font-medium text-gray-700 uppercase tracking-wider">Action</th>
                  </tr>
                </thead>
                <tbody className="bg-white divide-y divide-gray-200">
                  {pendingBills.slice(0, 5).map((bill) => (
                    <tr key={bill.id} className="hover:bg-gray-50">
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium text-gray-900">{bill.invoiceNo}</td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-900">{bill.customerName}</td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm text-gray-500">{new Date(bill.invoiceDate).toLocaleDateString()}</td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-semibold text-red-600">{formatCurrency(bill.balance)}</td>
                      <td className="px-6 py-4 whitespace-nowrap">
                        <span className={`px-2 py-1 text-xs font-medium rounded-full ${
                          (bill.daysOverdue || 0) > 30 
                            ? 'bg-red-100 text-red-800' 
                            : (bill.daysOverdue || 0) > 15 
                            ? 'bg-orange-100 text-orange-800'
                            : 'bg-yellow-100 text-yellow-800'
                        }`}>
                          {bill.daysOverdue || 0} days old
                        </span>
                      </td>
                      <td className="px-6 py-4 whitespace-nowrap text-sm font-medium">
                        <button
                          onClick={() => navigate(`/payments`)}
                          className="text-blue-600 hover:text-blue-900 font-medium"
                        >
                          Get Payment
                        </button>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default Dashboard
