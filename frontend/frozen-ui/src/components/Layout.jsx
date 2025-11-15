import { useState } from 'react'
import { Link, useLocation, Outlet, useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'
import { 
  Home, 
  Package, 
  ShoppingCart, 
  CreditCard, 
  Users, 
  DollarSign, 
  TrendingUp, 
  Settings, 
  Menu,
  X,
  LogOut,
  Bell,
  Search,
  DollarSign as PriceTag,
  Shield,
  BarChart3,
  Truck,
  FileText,
  BookOpen,
  Receipt
} from 'lucide-react'
import BottomNav from './BottomNav'
import Logo from './Logo'
import AlertNotifications from './AlertNotifications'

const Layout = () => {
  const { user, logout } = useAuth()
  const location = useLocation()
  const navigate = useNavigate()
  const [sidebarOpen, setSidebarOpen] = useState(false)

  const navigation = [
    { name: 'Dashboard', href: '/dashboard', icon: Home },
    { name: 'Products', href: '/products', icon: Package },
    { name: 'Purchases', href: '/purchases', icon: Truck },
    { name: 'POS', href: '/pos', icon: ShoppingCart },
    { name: 'Customer Ledger', href: '/ledger', icon: BookOpen },
    { name: 'Expenses', href: '/expenses', icon: Receipt },
    { name: 'Reports', href: '/reports', icon: BarChart3 },
    ...(user?.role?.toLowerCase() === 'admin' ? [{ name: 'Users', href: '/users', icon: Shield }] : []),
    ...(user?.role?.toLowerCase() === 'admin' ? [{ name: 'Settings', href: '/settings', icon: Settings }] : []),
    ...(user?.role?.toLowerCase() === 'admin' ? [{ name: 'Backup & Restore', href: '/backup', icon: FileText }] : []),
  ]

  const isActive = (href) => location.pathname === href

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Mobile Header with Hamburger Menu */}
      <div className="lg:hidden fixed top-0 left-0 right-0 bg-blue-800 text-white shadow-lg z-40">
        <div className="flex items-center justify-between px-4 py-3">
          <button
            onClick={() => setSidebarOpen(true)}
            className="p-2 rounded-lg hover:bg-blue-700 transition-colors"
            aria-label="Open menu"
          >
            <Menu className="h-6 w-6" />
          </button>
          <Logo size="small" className="flex-1 justify-center" />
          <div className="w-10"></div> {/* Spacer for centering */}
        </div>
      </div>

      {/* Mobile sidebar */}
      <div className={`fixed inset-0 z-50 lg:hidden transition-transform duration-300 ${sidebarOpen ? 'translate-x-0' : '-translate-x-full'}`}>
        <div className="fixed inset-0 bg-gray-600 bg-opacity-75" onClick={() => setSidebarOpen(false)} />
        <div className="fixed inset-y-0 left-0 flex w-64 flex-col bg-blue-800 text-white shadow-xl">
          <div className="flex h-16 items-center justify-between px-4">
            <Logo size="small" showText={false} className="text-white" />
            <button
              onClick={() => setSidebarOpen(false)}
              className="text-white hover:text-blue-200"
            >
              <X className="h-6 w-6" />
            </button>
          </div>
          <nav className="flex-1 px-4 py-4 space-y-1">
            {navigation.map((item) => {
              const Icon = item.icon
              return (
                <Link
                  key={item.name}
                  to={item.href}
                  onClick={() => setSidebarOpen(false)}
                  className={`flex items-center px-3 py-2 text-sm font-medium rounded-lg ${
                    isActive(item.href)
                      ? 'bg-blue-600 text-white'
                      : 'text-blue-100 hover:bg-blue-700'
                  }`}
                >
                  <Icon className="mr-3 h-5 w-5" />
                  {item.name}
                </Link>
              )
            })}
          </nav>
          <div className="border-t border-blue-700 p-4">
            <button
              onClick={logout}
              className="flex items-center text-sm text-blue-100 hover:text-white"
            >
              <LogOut className="mr-2 h-4 w-4" />
              Sign out
            </button>
          </div>
        </div>
      </div>

      {/* Desktop sidebar - Blue Theme */}
      <div className="hidden lg:fixed lg:inset-y-0 lg:flex lg:w-20 lg:flex-col">
        <div className="flex flex-col flex-grow bg-blue-800 text-white shadow-2xl">
          <nav className="flex-1 px-2 py-4 space-y-2">
            {navigation.map((item) => {
              const Icon = item.icon
              return (
                <Link
                  key={item.name}
                  to={item.href}
                  className={`flex flex-col items-center justify-center px-3 py-2 text-sm font-medium rounded-lg transition-colors ${
                    isActive(item.href)
                      ? 'bg-blue-600 text-white'
                      : 'text-blue-100 hover:bg-blue-700'
                  }`}
                  title={item.name}
                >
                  <Icon className="h-5 w-5 mb-1" />
                  <span className="text-xs text-center">{item.name}</span>
                </Link>
              )
            })}
          </nav>
          <div className="border-t border-blue-700 p-2">
            <button
              onClick={logout}
              className="flex flex-col items-center justify-center w-full px-3 py-2 text-sm text-blue-100 hover:text-white hover:bg-blue-700 rounded-lg transition-colors"
            >
              <LogOut className="h-5 w-5 mb-1" />
              <span className="text-xs">Logout</span>
            </button>
          </div>
        </div>
      </div>

      {/* Main content */}
      <div className="lg:pl-20">
        {/* Top Header Bar for Other Pages - Similar to Dashboard */}
        <div className="hidden lg:block fixed top-0 left-20 right-0 bg-gradient-to-r from-blue-900 to-blue-800 text-white shadow-lg z-30">
          <div className="flex items-center justify-between px-4 py-3">
            <div className="flex items-center space-x-3 flex-1 min-w-0">
              <Logo size="default" showText={false} className="flex-shrink-0" />
              <div className="min-w-0 flex-1">
                <h1 className="text-base xl:text-lg font-bold truncate">STARPLUS FOODSTUFF TRADING</h1>
                <p className="text-xs text-blue-200 hidden xl:block">Frozen Food Trading & Management System</p>
              </div>
            </div>
            <div className="flex items-center space-x-1.5 flex-shrink-0">
              {/* Alert Notifications - Admin Only */}
              {user?.role?.toLowerCase() === 'admin' && (
                <AlertNotifications />
              )}
              {/* Top Bar Icons - Admin Only */}
              {user?.role?.toLowerCase() === 'admin' && (
                <>
                  <button
                    onClick={() => navigate('/backup')}
                    className="p-2 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                    title="Backup & Restore"
                  >
                    <FileText className="h-5 w-5" />
                  </button>
                  <button
                    onClick={() => navigate('/settings')}
                    className="p-2 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                    title="Settings"
                  >
                    <Settings className="h-5 w-5" />
                  </button>
                  <button
                    onClick={() => navigate('/reports?tab=profit-loss')}
                    className="p-2 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                    title="Profit & Loss"
                  >
                    <TrendingUp className="h-5 w-5" />
                  </button>
                  <button
                    onClick={() => navigate('/users')}
                    className="p-2 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
                    title="Users"
                  >
                    <Users className="h-5 w-5" />
                  </button>
                </>
              )}
              <div className="text-right hidden md:block ml-2">
                <p className="text-xs font-medium">{new Date().toLocaleDateString('en-GB', { 
                  weekday: 'short', 
                  year: 'numeric', 
                  month: 'short', 
                  day: 'numeric' 
                })}</p>
                <p className="text-[10px] text-amber-200">{user?.name || 'User'}</p>
              </div>
              <button
                onClick={logout}
                className="px-2 py-1.5 bg-red-600 hover:bg-red-700 rounded-lg transition flex items-center space-x-1 text-xs cursor-pointer ml-2"
              >
                <LogOut className="h-4 w-4" />
                <span className="hidden sm:inline">Logout</span>
              </button>
            </div>
          </div>
        </div>
        {/* Page content */}
        <main className="flex-1 pb-20 lg:pb-6 pt-14 lg:pt-20">
          <div className="py-1 sm:py-2 lg:py-6">
            <div className="mx-auto max-w-7xl px-1.5 sm:px-2 lg:px-4 xl:px-8">
              <Outlet />
            </div>
          </div>
        </main>
        {/* Mobile Bottom Navigation */}
        <div className="lg:hidden">
          <BottomNav />
        </div>
      </div>
    </div>
  )
}

export default Layout
