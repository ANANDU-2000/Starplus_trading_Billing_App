import { useState, useEffect } from 'react'
import { Download, FileSpreadsheet, Share2 } from 'lucide-react'
import { formatCurrency } from '../utils/currency'
import toast from 'react-hot-toast'
import { LoadingCard } from '../components/Loading'
import { reportsAPI } from '../services'

const PERIODS = [
  { value: 'week', label: 'This Week' },
  { value: 'month', label: 'This Month' },
  { value: 'year', label: 'This Year' },
  { value: 'custom', label: 'Custom' }
]

const WorksheetPage = () => {
  const [loading, setLoading] = useState(true)
  const [period, setPeriod] = useState('month')
  const [customFrom, setCustomFrom] = useState('')
  const [customTo, setCustomTo] = useState('')
  const [data, setData] = useState(null)
  const [error, setError] = useState(null)

  const fetchWorksheet = async () => {
    setLoading(true)
    setError(null)
    try {
      const params = { period }
      if (period === 'custom') {
        if (!customFrom || !customTo) {
          toast.error('Please select From and To dates for Custom period')
          setLoading(false)
          return
        }
        params.fromDate = customFrom
        params.toDate = customTo
      }
      const res = await reportsAPI.getWorksheetReport(params)
      if (res?.success && res?.data) {
        setData(res.data)
      } else {
        setError(res?.message || 'Failed to load worksheet')
        setData(null)
      }
    } catch (err) {
      console.error('Worksheet fetch error:', err)
      setError(err?.response?.data?.message || err?.message || 'Failed to load worksheet')
      setData(null)
      toast.error('Failed to load worksheet')
    } finally {
      setLoading(false)
    }
  }

  useEffect(() => {
    if (period !== 'custom') {
      fetchWorksheet()
    } else if (customFrom && customTo) {
      fetchWorksheet()
    } else {
      setLoading(false)
      setData(null)
    }
  }, [period, customFrom, customTo])

  const handlePeriodChange = (p) => {
    setPeriod(p)
    if (p !== 'custom') setData(null)
  }

  const handleExportPdf = async () => {
    try {
      toast.loading('Generating PDF...')
      const params = { period }
      if (period === 'custom') {
        if (!customFrom || !customTo) {
          toast.dismiss()
          toast.error('Select From and To dates first')
          return
        }
        params.fromDate = customFrom
        params.toDate = customTo
      }
      const blob = await reportsAPI.exportWorksheetPdf(params)
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      const label = data?.periodLabel ?? data?.PeriodLabel ?? 'worksheet'
      a.download = `worksheet_${String(label).replace(/\s/g, '_')}.pdf`
      document.body.appendChild(a)
      a.click()
      a.remove()
      window.URL.revokeObjectURL(url)
      toast.dismiss()
      toast.success('PDF downloaded')
    } catch (err) {
      toast.dismiss()
      toast.error(err?.response?.data?.message || 'Failed to export PDF')
    }
  }

  const handleShare = () => {
    if (!data) return
    const label = data.periodLabel ?? data.PeriodLabel ?? 'Period'
    const s = data.totalSales ?? data.TotalSales ?? 0
    const p = data.totalPurchase ?? data.TotalPurchase ?? 0
    const e = data.totalExpenses ?? data.TotalExpenses ?? 0
    const pend = data.pendingAmount ?? data.PendingAmount ?? 0
    const text = [
      `Worksheet – ${label}`,
      `Sales: ${formatCurrency(s)}`,
      `Purchase: ${formatCurrency(p)}`,
      `Expenses: ${formatCurrency(e)}`,
      `Pending: ${formatCurrency(pend)}`
    ].join('\n')
    if (navigator.clipboard?.writeText) {
      navigator.clipboard.writeText(text).then(() => toast.success('Summary copied')).catch(() => toast.error('Copy failed'))
    } else {
      const wa = `https://wa.me/?text=${encodeURIComponent(text)}`
      window.open(wa, '_blank')
    }
  }

  const d = data || {}
  const fromDate = (d.fromDate != null) ? new Date(d.fromDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : ''
  const toDate = (d.toDate != null) ? new Date(d.toDate).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : ''
  const totalSales = d.totalSales ?? d.TotalSales ?? 0
  const totalPurchase = d.totalPurchase ?? d.TotalPurchase ?? 0
  const totalExpenses = d.totalExpenses ?? d.TotalExpenses ?? 0
  const pendingAmount = d.pendingAmount ?? d.PendingAmount ?? 0
  const receivedInPeriod = d.receivedInPeriod ?? d.ReceivedInPeriod ?? 0
  const periodLabel = d.periodLabel ?? d.PeriodLabel ?? ''

  return (
    <div className="flex flex-col h-full max-w-4xl mx-auto">
      <div className="flex-shrink-0 flex flex-col sm:flex-row sm:items-center sm:justify-between gap-3 pb-4 border-b border-gray-200">
        <div>
          <h1 className="text-xl font-bold text-gray-900 flex items-center gap-2">
            <FileSpreadsheet className="w-6 h-6 text-blue-600" />
            Worksheet
          </h1>
          <p className="text-sm text-gray-500 mt-0.5">Share period summary with partners</p>
        </div>
        <div className="flex flex-wrap items-center gap-2">
          <div className="flex flex-wrap gap-1">
            {PERIODS.map(({ value, label }) => (
              <button
                key={value}
                onClick={() => handlePeriodChange(value)}
                className={`px-3 py-1.5 rounded-md text-sm font-medium ${
                  period === value ? 'bg-blue-600 text-white' : 'bg-white border border-gray-300 text-gray-700 hover:bg-gray-50'
                }`}
              >
                {label}
              </button>
            ))}
          </div>
          {period === 'custom' && (
            <div className="flex gap-2 items-center">
              <input
                type="date"
                value={customFrom}
                onChange={(e) => setCustomFrom(e.target.value)}
                className="px-2 py-1.5 border border-gray-300 rounded text-sm"
              />
              <span className="text-gray-500">to</span>
              <input
                type="date"
                value={customTo}
                onChange={(e) => setCustomTo(e.target.value)}
                className="px-2 py-1.5 border border-gray-300 rounded text-sm"
              />
            </div>
          )}
          <button
            onClick={fetchWorksheet}
            disabled={loading || (period === 'custom' && (!customFrom || !customTo))}
            className="px-3 py-1.5 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 disabled:opacity-50"
          >
            Refresh
          </button>
          {data && (
            <>
              <button
                onClick={handleExportPdf}
                className="px-3 py-1.5 bg-blue-600 text-white rounded-md text-sm font-medium hover:bg-blue-700 flex items-center gap-1.5"
              >
                <Download className="w-4 h-4" />
                PDF
              </button>
              <button
                onClick={handleShare}
                className="px-3 py-1.5 border border-gray-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-gray-50 flex items-center gap-1.5"
                title="Copy summary or share via WhatsApp"
              >
                <Share2 className="w-4 h-4" />
                Share
              </button>
            </>
          )}
        </div>
      </div>

      {loading && (
        <div className="flex-1 flex items-center justify-center py-12">
          <LoadingCard message="Loading worksheet..." />
        </div>
      )}

      {!loading && error && (
        <div className="flex-1 flex items-center justify-center py-12">
          <p className="text-red-600">{error}</p>
        </div>
      )}

      {!loading && !error && data && (
        <div className="flex-1 py-6">
          <div className="mb-4">
            <h2 className="text-lg font-semibold text-gray-800">{periodLabel || 'Period'}</h2>
            <p className="text-sm text-gray-500">{fromDate} – {toDate}</p>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-4">
            <div className="bg-blue-50 rounded-lg p-4 border-l-4 border-blue-500">
              <div className="text-xs font-medium text-gray-600 uppercase tracking-wide">Total Sales</div>
              <div className="text-xl font-bold text-gray-900 mt-1">{formatCurrency(totalSales)}</div>
            </div>
            <div className="bg-amber-50 rounded-lg p-4 border-l-4 border-amber-500">
              <div className="text-xs font-medium text-gray-600 uppercase tracking-wide">Total Purchase</div>
              <div className="text-xl font-bold text-gray-900 mt-1">{formatCurrency(totalPurchase)}</div>
            </div>
            <div className="bg-rose-50 rounded-lg p-4 border-l-4 border-rose-500">
              <div className="text-xs font-medium text-gray-600 uppercase tracking-wide">Total Expenses</div>
              <div className="text-xl font-bold text-gray-900 mt-1">{formatCurrency(totalExpenses)}</div>
            </div>
            <div className="bg-orange-50 rounded-lg p-4 border-l-4 border-orange-500">
              <div className="text-xs font-medium text-gray-600 uppercase tracking-wide">Pending Amount</div>
              <div className="text-xl font-bold text-orange-700 mt-1">{formatCurrency(pendingAmount)}</div>
            </div>
          </div>
          {(receivedInPeriod != null && receivedInPeriod > 0) && (
            <div className="mt-4 bg-green-50 rounded-lg p-4 border-l-4 border-green-500 max-w-xs">
              <div className="text-xs font-medium text-gray-600 uppercase tracking-wide">Received in Period</div>
              <div className="text-lg font-bold text-gray-900 mt-1">{formatCurrency(receivedInPeriod)}</div>
            </div>
          )}
        </div>
      )}

      {!loading && !error && !data && period !== 'custom' && (
        <div className="flex-1 flex items-center justify-center py-12 text-gray-500">Select a period to view the worksheet.</div>
      )}

      {!loading && period === 'custom' && !customFrom && !customTo && (
        <div className="flex-1 flex items-center justify-center py-12 text-gray-500">Select From and To dates, then click Refresh.</div>
      )}
    </div>
  )
}

export default WorksheetPage
