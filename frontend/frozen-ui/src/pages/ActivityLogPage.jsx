import { useState, useEffect, useCallback, useMemo } from 'react'
import {
  Activity,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  RefreshCw,
  FileText,
  Users,
  Edit3
} from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { adminAPI, salesAPI } from '../services'
import { LoadingCard } from '../components/Loading'
import FilterPanel from '../components/ui/FilterPanel'
import ModernTable from '../components/ui/ModernTable'
import StatCard from '../components/ui/StatCard'
import Modal from '../components/Modal'
import toast from 'react-hot-toast'

const ACTION_OPTIONS = [
  { label: 'Sales / Invoices', options: [
    { value: 'Sale Created', label: 'Sale Created' },
    { value: 'Sale Created (Admin Override)', label: 'Sale Created (Admin Override)' },
    { value: 'Sale Updated', label: 'Sale Updated' },
    { value: 'Sale Deleted', label: 'Sale Deleted' },
    { value: 'Invoice Edit - Paid Amount Adjustment', label: 'Invoice Edit - Paid Amount Adjustment' },
    { value: 'Invoice Unlocked', label: 'Invoice Unlocked' },
    { value: 'Invoice Version Restored', label: 'Invoice Version Restored' }
  ]},
  { label: 'Payments', options: [
    { value: 'Payment Created', label: 'Payment Created' },
    { value: 'Payment Updated', label: 'Payment Updated' },
    { value: 'Payment Deleted', label: 'Payment Deleted' },
    { value: 'Payment Status Updated', label: 'Payment Status Updated' },
    { value: 'Bulk Payment Allocated', label: 'Bulk Payment Allocated' }
  ]},
  { label: 'Purchases / Returns', options: [
    { value: 'Purchase Created', label: 'Purchase Created' },
    { value: 'Purchase Deleted', label: 'Purchase Deleted' },
    { value: 'Purchase Return Created', label: 'Purchase Return Created' },
    { value: 'Sale Return Created', label: 'Sale Return Created' }
  ]},
  { label: 'Expenses', options: [
    { value: 'Expense Created', label: 'Expense Created' },
    { value: 'Expense Updated', label: 'Expense Updated' },
    { value: 'Expense Deleted', label: 'Expense Deleted' },
    { value: 'Expense Bulk VAT Update', label: 'Expense Bulk VAT Update' }
  ]},
  { label: 'Stock', options: [
    { value: 'Stock Adjusted', label: 'Stock Adjusted' },
    { value: 'Stock Adjustment', label: 'Stock Adjustment' }
  ]},
  { label: 'Users / System', options: [
    { value: 'User Created', label: 'User Created' },
    { value: 'User Updated', label: 'User Updated' },
    { value: 'User Deleted', label: 'User Deleted' },
    { value: 'Password Reset', label: 'Password Reset' },
    { value: 'Customer Force Deleted', label: 'Customer Force Deleted' },
    { value: 'Backup Created', label: 'Backup Created' },
    { value: 'Backup Deleted', label: 'Backup Deleted' },
    { value: 'Backup Restored', label: 'Backup Restored' },
    { value: 'SYSTEM_RESET', label: 'SYSTEM_RESET' }
  ]}
]

const DATE_PRESETS = [
  { value: '', label: 'All time' },
  { value: 'today', label: 'Today' },
  { value: '7d', label: 'Last 7 days' },
  { value: '30d', label: 'Last 30 days' }
]

function parseInvoiceNoFromDetails (details) {
  if (!details) return null
  const match = details.match(/Invoice:?\s*([A-Za-z0-9-]+)/i)
  return match ? match[1] : null
}

function formatRelativeTime (dateStr) {
  const d = new Date(dateStr)
  const diffMs = Date.now() - d.getTime()
  const mins = Math.floor(diffMs / 60000)
  if (mins < 1) return 'Just now'
  if (mins < 60) return `${mins}m ago`
  const hours = Math.floor(mins / 60)
  if (hours < 24) return `${hours}h ago`
  const days = Math.floor(hours / 24)
  if (days < 7) return `${days}d ago`
  return d.toLocaleDateString()
}

function actionPillClass (action) {
  if (!action) return 'bg-gray-100 text-gray-700'
  const lower = action.toLowerCase()
  if (lower.includes('created') || lower.includes('restored')) return 'bg-green-100 text-green-800'
  if (lower.includes('deleted') || lower.includes('failed') || lower.includes('reset')) return 'bg-red-100 text-red-800'
  if (lower.includes('updated') || lower.includes('edit') || lower.includes('unlocked') || lower.includes('adjusted')) return 'bg-blue-100 text-blue-800'
  return 'bg-gray-100 text-gray-700'
}

function getDateRange (preset) {
  if (!preset) return { fromDate: undefined, toDate: undefined }
  const now = new Date()
  const end = new Date(now)
  end.setHours(23, 59, 59, 999)
  let start
  if (preset === 'today') {
    start = new Date(now)
    start.setHours(0, 0, 0, 0)
  } else if (preset === '7d') {
    start = new Date(now)
    start.setDate(start.getDate() - 7)
    start.setHours(0, 0, 0, 0)
  } else if (preset === '30d') {
    start = new Date(now)
    start.setDate(start.getDate() - 30)
    start.setHours(0, 0, 0, 0)
  } else {
    return { fromDate: undefined, toDate: undefined }
  }
  return { fromDate: start.toISOString(), toDate: end.toISOString() }
}

const ActivityLogPage = () => {
  const { user: currentUser } = useAuth()
  const [loading, setLoading] = useState(true)
  const [logs, setLogs] = useState([])
  const [users, setUsers] = useState([])
  const [page, setPage] = useState(1)
  const [pageSize] = useState(20)
  const [totalPages, setTotalPages] = useState(1)
  const [totalCount, setTotalCount] = useState(0)
  const [searchInput, setSearchInput] = useState('')
  const [debouncedSearch, setDebouncedSearch] = useState('')
  const [filters, setFilters] = useState({ action: '', userId: '', datePreset: '' })
  const [selectedLog, setSelectedLog] = useState(null)
  const [detailLoading, setDetailLoading] = useState(false)
  const [versionDetail, setVersionDetail] = useState(null)

  const fetchLogs = useCallback(async () => {
    try {
      setLoading(true)
      const { fromDate, toDate } = getDateRange(filters.datePreset)
      const params = {
        page,
        pageSize,
        ...(filters.action && { action: filters.action }),
        ...(filters.userId && { userId: Number(filters.userId) }),
        ...(fromDate && { fromDate }),
        ...(toDate && { toDate }),
        ...(debouncedSearch.trim() && { search: debouncedSearch.trim() })
      }
      const response = await adminAPI.getAuditLogs(params)
      if (response?.success && response?.data) {
        setLogs(response.data.items || [])
        setTotalCount(response.data.totalCount || 0)
        setTotalPages(response.data.totalPages || 1)
      } else {
        setLogs([])
        setTotalCount(0)
        setTotalPages(1)
      }
    } catch (error) {
      console.error('Error loading activity log:', error)
      toast.error('Failed to load activity log')
      setLogs([])
    } finally {
      setLoading(false)
    }
  }, [page, pageSize, filters, debouncedSearch])

  useEffect(() => {
    const timer = setTimeout(() => {
      setDebouncedSearch(searchInput)
      setPage(1)
    }, 400)
    return () => clearTimeout(timer)
  }, [searchInput])

  const fetchUsers = useCallback(async () => {
    try {
      const response = await adminAPI.getUsers({ pageSize: 200 })
      if (response?.success && response?.data) {
        setUsers(response.data.items || [])
      }
    } catch (error) {
      console.error('Error loading users for filter:', error)
    }
  }, [])

  useEffect(() => {
    if (currentUser?.role?.toLowerCase() === 'admin') {
      fetchUsers()
    }
  }, [currentUser, fetchUsers])

  useEffect(() => {
    if (currentUser?.role?.toLowerCase() === 'admin') {
      fetchLogs()
    }
  }, [currentUser, fetchLogs])

  const stats = useMemo(() => {
    const todayStart = new Date()
    todayStart.setHours(0, 0, 0, 0)
    const actionsToday = logs.filter(l => new Date(l.createdAt) >= todayStart).length
    const invoiceEdits = logs.filter(l =>
      l.action && (l.action.startsWith('Sale') || l.action.startsWith('Invoice'))
    ).length
    const uniqueUsers = new Set(logs.map(l => l.userName).filter(Boolean)).size
    return { actionsToday, invoiceEdits, uniqueUsers, pageTotal: logs.length }
  }, [logs])

  const openDetail = async (log) => {
    setSelectedLog(log)
    setVersionDetail(null)
    const isInvoiceAction = log.action && (log.action.startsWith('Sale') || log.action.startsWith('Invoice'))
    if (!isInvoiceAction) return

    const invoiceNo = parseInvoiceNoFromDetails(log.details)
    if (!invoiceNo) return

    setDetailLoading(true)
    try {
      const salesRes = await salesAPI.getSales({ search: invoiceNo, pageSize: 5 })
      const sale = salesRes?.data?.items?.find(s => String(s.invoiceNo) === String(invoiceNo))
      if (!sale?.id) return

      const versionsRes = await salesAPI.getInvoiceVersions(sale.id)
      if (versionsRes?.success && versionsRes?.data?.length) {
        const latest = versionsRes.data[0]
        setVersionDetail({
          invoiceNo: sale.invoiceNo,
          diffSummary: latest.diffSummary,
          editReason: latest.editReason,
          versionNumber: latest.versionNumber,
          createdAt: latest.createdAt
        })
      }
    } catch (err) {
      console.warn('Could not load invoice version detail:', err)
    } finally {
      setDetailLoading(false)
    }
  }

  const closeDetail = () => {
    setSelectedLog(null)
    setVersionDetail(null)
  }

  const filterConfig = [
    {
      key: 'action',
      label: 'All actions',
      options: ACTION_OPTIONS.flatMap(g => g.options)
    },
    {
      key: 'userId',
      label: 'All users',
      options: users.map(u => ({ value: String(u.id), label: u.name || u.email }))
    },
    {
      key: 'datePreset',
      label: 'Date range',
      options: DATE_PRESETS.map(p => ({ value: p.value, label: p.label }))
    }
  ]

  const columns = [
    {
      key: 'createdAt',
      label: 'Time',
      sortable: true,
      render: (row) => (
        <span title={new Date(row.createdAt).toLocaleString()}>
          {formatRelativeTime(row.createdAt)}
        </span>
      )
    },
    {
      key: 'userName',
      label: 'User',
      sortable: true,
      render: (row) => row.userName || '—'
    },
    {
      key: 'action',
      label: 'Action',
      sortable: true,
      render: (row) => (
        <span className={`inline-flex px-2 py-0.5 rounded-full text-xs font-medium ${actionPillClass(row.action)}`}>
          {row.action}
        </span>
      )
    },
    {
      key: 'details',
      label: 'Details',
      render: (row) => {
        const text = row.details || '—'
        return (
          <span className="block max-w-md truncate" title={text}>
            {text}
          </span>
        )
      }
    }
  ]

  if (currentUser?.role?.toLowerCase() !== 'admin') {
    return (
      <div className="min-h-screen bg-gradient-to-br from-blue-50 to-slate-50 flex items-center justify-center p-4">
        <div className="bg-white rounded-lg shadow-lg p-8 text-center max-w-md">
          <AlertCircle className="h-16 w-16 text-red-500 mx-auto mb-4" />
          <h2 className="text-xl font-bold text-gray-900 mb-2">Access Denied</h2>
          <p className="text-gray-600">Only administrators can access the activity log.</p>
        </div>
      </div>
    )
  }

  if (loading && logs.length === 0) {
    return <LoadingCard message="Loading activity log..." />
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-slate-50">
      <div className="p-6">
        <div className="flex flex-col sm:flex-row sm:items-center sm:justify-between mb-6">
          <div>
            <h1 className="text-2xl font-bold text-gray-900 flex items-center">
              <Activity className="h-6 w-6 mr-2 text-blue-600" />
              Activity Log
            </h1>
            <p className="text-gray-600">Who did what, and when — invoice edits, payments, stock, and more</p>
          </div>
          <button
            type="button"
            onClick={() => fetchLogs()}
            className="mt-4 sm:mt-0 px-4 py-2 bg-blue-600 hover:bg-blue-700 text-white rounded-lg transition flex items-center"
          >
            <RefreshCw className="h-4 w-4 mr-2" />
            Refresh
          </button>
        </div>

        <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4 mb-6">
          <StatCard title="On this page" value={stats.pageTotal} icon={FileText} iconColor="blue" format="number" />
          <StatCard title="Actions today (page)" value={stats.actionsToday} icon={Activity} iconColor="green" format="number" />
          <StatCard title="Invoice/sale rows (page)" value={stats.invoiceEdits} icon={Edit3} iconColor="purple" format="number" />
          <StatCard title="Users (page)" value={stats.uniqueUsers} icon={Users} iconColor="orange" format="number" />
        </div>

        <FilterPanel
          searchPlaceholder="Search details (e.g. INV-0042 or 6033)..."
          onSearchChange={(v) => setSearchInput(v)}
          filters={filterConfig}
          activeFilters={filters}
          onFilterChange={(next) => {
            setFilters(next)
            setPage(1)
          }}
        />

        <ModernTable
          data={logs}
          columns={columns}
          loading={loading}
          onRowClick={openDetail}
        />

        <div className="flex items-center justify-between mt-4 bg-white rounded-lg border border-gray-200 px-4 py-3">
          <p className="text-sm text-gray-600">
            {totalCount} total entries · Page {page} of {totalPages}
          </p>
          <div className="flex items-center gap-2">
            <button
              type="button"
              disabled={page <= 1}
              onClick={() => setPage(p => Math.max(1, p - 1))}
              className="p-2 rounded-lg border border-gray-300 disabled:opacity-40 hover:bg-gray-50"
            >
              <ChevronLeft className="h-5 w-5" />
            </button>
            <button
              type="button"
              disabled={page >= totalPages}
              onClick={() => setPage(p => p + 1)}
              className="p-2 rounded-lg border border-gray-300 disabled:opacity-40 hover:bg-gray-50"
            >
              <ChevronRight className="h-5 w-5" />
            </button>
          </div>
        </div>
      </div>

      <Modal isOpen={!!selectedLog} onClose={closeDetail} title="Activity detail" size="lg">
        {selectedLog && (
          <div className="space-y-4">
            <div className="grid grid-cols-2 gap-3 text-sm">
              <div>
                <p className="text-gray-500">Time</p>
                <p className="font-medium">{new Date(selectedLog.createdAt).toLocaleString()}</p>
              </div>
              <div>
                <p className="text-gray-500">User</p>
                <p className="font-medium">{selectedLog.userName || '—'}</p>
              </div>
              <div className="col-span-2">
                <p className="text-gray-500">Action</p>
                <span className={`inline-flex mt-1 px-2 py-0.5 rounded-full text-xs font-medium ${actionPillClass(selectedLog.action)}`}>
                  {selectedLog.action}
                </span>
              </div>
            </div>

            <div>
              <p className="text-gray-500 text-sm mb-1">Details</p>
              <p className="text-sm text-gray-900 whitespace-pre-wrap bg-gray-50 rounded-lg p-3 border border-gray-200">
                {selectedLog.details || '—'}
              </p>
            </div>

            {detailLoading && (
              <p className="text-sm text-gray-500">Loading invoice change history…</p>
            )}

            {versionDetail && (
              <div className="border-t border-gray-200 pt-4">
                <p className="text-sm font-semibold text-gray-900 mb-2">
                  Invoice {versionDetail.invoiceNo} — version {versionDetail.versionNumber}
                </p>
                {versionDetail.editReason && (
                  <p className="text-sm text-gray-700 mb-2">
                    <span className="font-medium">Edit reason:</span> {versionDetail.editReason}
                  </p>
                )}
                {versionDetail.diffSummary && (
                  <pre className="text-xs text-gray-800 bg-blue-50 rounded-lg p-3 border border-blue-100 whitespace-pre-wrap overflow-x-auto">
                    {versionDetail.diffSummary}
                  </pre>
                )}
              </div>
            )}
          </div>
        )}
      </Modal>
    </div>
  )
}

export default ActivityLogPage
