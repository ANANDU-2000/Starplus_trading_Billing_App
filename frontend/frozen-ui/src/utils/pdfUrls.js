const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5001/api'

function getAuthToken () {
  try {
    return localStorage.getItem('token') || ''
  } catch {
    return ''
  }
}

function buildQuery (params) {
  const qs = new URLSearchParams()
  Object.entries(params).forEach(([key, value]) => {
    if (value != null && value !== '') qs.set(key, String(value))
  })
  return qs.toString()
}

/** Normalize to YYYY-MM-DD for API date filters. */
function toDateOnly (value) {
  if (!value) return ''
  if (typeof value === 'string') {
    const m = value.match(/^(\d{4}-\d{2}-\d{2})/)
    if (m) return m[1]
    const d = new Date(value)
    if (!Number.isNaN(d.getTime())) return d.toISOString().split('T')[0]
    return value
  }
  if (value instanceof Date && !Number.isNaN(value.getTime())) {
    return value.toISOString().split('T')[0]
  }
  return String(value)
}

function withAuth (url) {
  const token = getAuthToken()
  if (!token) return url
  const sep = url.includes('?') ? '&' : '?'
  return `${url}${sep}access_token=${encodeURIComponent(token)}`
}

function pdfFlags ({ print = false, open = false } = {}) {
  const flags = {}
  if (print) flags.print = '1'
  if (open) flags.open = '1'
  return flags
}

export function getApiBaseUrl () {
  return API_BASE_URL
}

export function getHealthUrl () {
  const base = API_BASE_URL.replace(/\/api\/?$/, '')
  return `${base}/api/health`
}

export function getInvoicePdfUrl (saleId, opts = {}) {
  const qs = buildQuery(pdfFlags(opts))
  return `${API_BASE_URL}/sales/${saleId}/pdf${qs ? `?${qs}` : ''}`
}

export function getReceiptPdfUrl (receiptId, opts = {}) {
  const qs = buildQuery(pdfFlags(opts))
  return `${API_BASE_URL}/payments/receipt/${receiptId}/pdf${qs ? `?${qs}` : ''}`
}

export function getStatementPdfUrl (customerId, fromDate, toDate, opts = {}) {
  const qs = buildQuery({
    fromDate: toDateOnly(fromDate),
    toDate: toDateOnly(toDate),
    ...pdfFlags(opts)
  })
  return withAuth(`${API_BASE_URL}/customers/${customerId}/statement?${qs}`)
}

export function getPendingBillsPdfUrl (customerId, fromDate, toDate, opts = {}) {
  const qs = buildQuery({
    fromDate: toDateOnly(fromDate),
    toDate: toDateOnly(toDate),
    ...pdfFlags(opts)
  })
  return withAuth(`${API_BASE_URL}/customers/${customerId}/pending-bills-pdf?${qs}`)
}

export function getWorksheetPdfUrl (params = {}, opts = {}) {
  const qs = buildQuery({ ...params, ...pdfFlags(opts) })
  return withAuth(`${API_BASE_URL}/reports/worksheet/export/pdf?${qs}`)
}

export function getPendingBillsReportPdfUrl (params = {}, opts = {}) {
  const qs = buildQuery({ ...params, ...pdfFlags(opts) })
  return withAuth(`${API_BASE_URL}/reports/pending-bills/export/pdf?${qs}`)
}

export function getSalesLedgerPdfUrl (params = {}, opts = {}) {
  const qs = buildQuery({ ...params, ...pdfFlags(opts) })
  return withAuth(`${API_BASE_URL}/reports/sales-ledger/export/pdf?${qs}`)
}
