import {
  Home,
  Package,
  ShoppingCart,
  Settings,
  Shield,
  BarChart3,
  Truck,
  FileText,
  BookOpen,
  Receipt,
  ClipboardList,
  Activity,
  Database,
  TrendingUp,
  DollarSign,
  Users
} from 'lucide-react'

export function isNavActive(pathname, href) {
  if (href === '/reports') {
    return pathname === '/reports' || pathname.startsWith('/reports/')
  }
  return pathname === href
}

export function getMainNavigation(user) {
  const isAdmin = user?.role?.toLowerCase() === 'admin'
  return [
    { name: 'Dashboard', href: '/dashboard', icon: Home },
    { name: 'Products', href: '/products', icon: Package },
    { name: 'Purchases', href: '/purchases', icon: Truck },
    { name: 'POS', href: '/pos', icon: ShoppingCart },
    { name: 'Customer Ledger', href: '/ledger', icon: BookOpen },
    { name: 'Expenses', href: '/expenses', icon: Receipt },
    { name: 'Sales Ledger', href: '/sales-ledger', icon: FileText },
    { name: 'Reports', href: '/reports', icon: BarChart3 },
    { name: 'Worksheet', href: '/worksheet', icon: ClipboardList },
    ...(isAdmin ? [{ name: 'Users', href: '/users', icon: Shield }] : []),
    ...(isAdmin ? [{ name: 'Activity Log', href: '/activity-log', icon: Activity }] : []),
    ...(isAdmin ? [{ name: 'Settings', href: '/settings', icon: Settings }] : []),
    ...(isAdmin ? [{ name: 'Backup & Restore', href: '/backup', icon: FileText }] : []),
  ]
}

export function getGatewayMenu(user) {
  const isAdmin = user?.role?.toLowerCase() === 'admin'
  return [
    {
      title: 'MASTERS',
      items: [
        { icon: Package, label: 'Products', path: '/products', shortcut: 'F1' }
      ]
    },
    {
      title: 'TRANSACTIONS',
      items: [
        { icon: ShoppingCart, label: 'POS Billing', path: '/pos', shortcut: 'F3', primary: true },
        { icon: Truck, label: 'Purchases', path: '/purchases', shortcut: 'F4' },
        { icon: Receipt, label: 'Expenses', path: '/expenses', shortcut: 'F5' },
        { icon: BookOpen, label: 'Customer Ledger', path: '/ledger', shortcut: 'F6' },
        { icon: FileText, label: 'Sales Ledger', path: '/sales-ledger', shortcut: 'F10' }
      ]
    },
    {
      title: 'REPORTS',
      items: [
        { icon: BarChart3, label: 'Sales Report', path: '/reports?tab=sales', shortcut: 'F7' },
        { icon: TrendingUp, label: 'Profit & Loss', path: '/reports?tab=profit-loss', shortcut: 'F8' },
        { icon: DollarSign, label: 'Outstanding Bills', path: '/reports?tab=outstanding', shortcut: 'F9' }
      ]
    },
    {
      title: 'UTILITIES',
      items: [
        ...(isAdmin ? [{ icon: Settings, label: 'Settings', path: '/settings', shortcut: 'Ctrl+S', adminOnly: true }] : []),
        ...(isAdmin ? [{ icon: Database, label: 'Backup & Restore', path: '/backup', shortcut: 'Ctrl+B', adminOnly: true }] : []),
        ...(isAdmin ? [{ icon: Users, label: 'Users', path: '/users', shortcut: 'Ctrl+U', adminOnly: true }] : []),
      ]
    }
  ]
}

export const bottomNavItems = [
  { name: 'Dashboard', href: '/dashboard', icon: Home },
  { name: 'POS', href: '/pos', icon: ShoppingCart },
  { name: 'Products', href: '/products', icon: Package },
  { name: 'Reports', href: '/reports', icon: BarChart3 },
  { name: 'Ledger', href: '/ledger', icon: BookOpen },
]
