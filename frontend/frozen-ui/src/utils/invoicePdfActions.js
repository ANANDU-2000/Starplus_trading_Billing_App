import toast from 'react-hot-toast'
import { salesAPI, paymentsAPI, customersAPI } from '../services'
import { triggerBlobDownload, isIOSDevice, isLikelyMobileBrowser } from './blobDownload'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'
const recentOpenByKey = new Map()
const DEBOUNCE_MS = 700

function safeInvoiceName (saleId, invoiceNo) {
  return `INV-${String(invoiceNo || saleId || 'invoice').replace(/[^\w.-]+/g, '_')}.pdf`
}

function safeReceiptName (receiptId, receiptNo) {
  return `Receipt-${String(receiptNo || receiptId || 'receipt').replace(/[^\w.-]+/g, '_')}.pdf`
}

function safeStatementName (customerName, suffix = 'statement') {
  const safe = String(customerName || 'customer').replace(/[^\w.-]+/g, '_')
  return `Ledger_${safe}_${new Date().toISOString().split('T')[0]}_${suffix}.pdf`
}

export function isStandalonePwaMode () {
  try {
    return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
  } catch {
    return false
  }
}

function buildUrl (path, { print = false, open = false, params = {} } = {}) {
  const search = new URLSearchParams()
  if (print) search.set('print', '1')
  if (open) search.set('open', '1')
  Object.entries(params).forEach(([key, value]) => {
    if (value != null && value !== '') search.set(key, String(value))
  })
  search.set('_', Date.now().toString())
  return `${API_BASE_URL}${path}?${search.toString()}`
}

export function invoicePdfUrl (saleId, { print = false, open = false, format, width } = {}) {
  const params = {}
  if (format) params.format = format
  if (width) params.width = width
  return buildUrl(`/sales/${saleId}/pdf`, { print, open, params })
}

export function receiptPdfUrl (receiptId, { print = false, open = false } = {}) {
  return buildUrl(`/payments/receipt/${receiptId}/pdf`, { print, open })
}

export function statementPdfUrl (customerId, fromDate, toDate, { print = false, open = false } = {}) {
  return buildUrl(`/customers/${customerId}/statement`, {
    print,
    open,
    params: { fromDate, toDate }
  })
}

export function pendingBillsPdfUrl (customerId, fromDate, toDate, { print = false, open = false } = {}) {
  return buildUrl(`/customers/${customerId}/pending-bills-pdf`, {
    print,
    open,
    params: { fromDate, toDate }
  })
}

function openPdfWithFallback (url, { forPrint = false } = {}) {
  const inPwa = isStandalonePwaMode()
  console.debug(`[pdf-open] attempt: forPrint=${forPrint}, pwa=${inPwa}, url=${url}`)

  const popup = window.open(url, '_blank', 'noopener,noreferrer')
  if (popup) {
    console.debug('[pdf-open] strategy: new-tab')
    return 'new-tab'
  }

  window.location.assign(url)
  console.debug('[pdf-open] strategy: same-tab-fallback')
  return 'same-tab'
}

function shouldDebounce (key) {
  const now = Date.now()
  const last = recentOpenByKey.get(key) || 0
  if (now - last < DEBOUNCE_MS) {
    console.debug(`[pdf-open] ignored duplicate click: ${key}`)
    return true
  }
  recentOpenByKey.set(key, now)
  return false
}

function openPdfUrlForPrint (url, { debounceKey, successMessage } = {}) {
  if (debounceKey && shouldDebounce(debounceKey)) return false

  const method = openPdfWithFallback(url, { forPrint: true })
  if (method === 'new-tab') {
    toast.success(
      successMessage ||
        (isIOSDevice()
          ? 'PDF opened. Tap Share, then Print.'
          : 'PDF opened for printing.')
    )
    return true
  }

  toast('Opened PDF in this tab for printing. Use browser print, then go back to app.', {
    icon: 'i',
    duration: 5500
  })
  return true
}

function openPdfUrlForViewing (url, { debounceKey } = {}) {
  if (debounceKey && shouldDebounce(debounceKey)) return false

  const method = openPdfWithFallback(url, { forPrint: false })
  if (method === 'same-tab') {
    toast('Opened PDF in this tab. Use browser menu to print or save, then go back.', {
      icon: 'i',
      duration: 5500
    })
  }
  return true
}

async function downloadPdfViaBlob (fetchFn, filename, { toastId = 'pdf-download' } = {}) {
  try {
    toast.loading('Preparing PDF...', { id: toastId })
    const response = await fetchFn()
    const raw = response instanceof Blob ? response : new Blob([response], { type: 'application/pdf' })
    const check = await validatePdfBlob(raw)
    if (!check.ok) {
      toast.error(check.message, { id: toastId })
      return false
    }

    triggerBlobDownload(check.blob, filename)
    toast.success('Download started - check your downloads folder', { id: toastId })
    return true
  } catch (error) {
    console.error('Failed to download PDF:', error)
    const msg = await parseApiErrorBlobMessage(error, 'Failed to download PDF')
    toast.error(msg, { id: toastId })
    return false
  }
}

async function downloadOrOpenPdf ({ urlForOpen, fetchFn, filename, toastId }) {
  if (isLikelyMobileBrowser() || isStandalonePwaMode()) {
    openPdfUrlForViewing(urlForOpen, { debounceKey: `view:${urlForOpen}` })
    toast.success(
      isIOSDevice()
        ? 'PDF opened. Tap Share to Save to Files or Print.'
        : 'PDF opened. Use the browser menu to download or print.'
    )
    return true
  }
  return downloadPdfViaBlob(fetchFn, filename, { toastId })
}

export function openInvoicePdfForPrint (saleId, { format, width } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  const url = invoicePdfUrl(saleId, { print: true, open: true, format, width })
  return openPdfUrlForPrint(url, {
    debounceKey: `print:invoice:${saleId}`,
    successMessage: isIOSDevice()
      ? 'Invoice opened. Tap Share, then Print.'
      : 'Invoice opened for printing.'
  })
}

export function openInvoicePdfForViewing (saleId) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  const url = invoicePdfUrl(saleId, { open: true })
  return openPdfUrlForViewing(url, { debounceKey: `view:invoice:${saleId}` })
}

export async function downloadInvoicePdf (saleId, invoiceNo, { toastId = 'invoice-pdf-download' } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot download invoice.')
    return false
  }

  return downloadOrOpenPdf({
    urlForOpen: invoicePdfUrl(saleId, { open: true }),
    fetchFn: () => salesAPI.getInvoicePdf(saleId),
    filename: safeInvoiceName(saleId, invoiceNo),
    toastId
  })
}

export function openReceiptPdfForPrint (receiptId) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot print receipt.')
    return false
  }
  const url = receiptPdfUrl(receiptId, { print: true, open: true })
  return openPdfUrlForPrint(url, {
    debounceKey: `print:receipt:${receiptId}`,
    successMessage: isIOSDevice()
      ? 'Receipt opened. Tap Share, then Print.'
      : 'Receipt opened for printing.'
  })
}

export function openReceiptPdfForViewing (receiptId) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot open receipt.')
    return false
  }
  const url = receiptPdfUrl(receiptId, { open: true })
  return openPdfUrlForViewing(url, { debounceKey: `view:receipt:${receiptId}` })
}

export async function downloadReceiptPdf (receiptId, receiptNo, { toastId = 'receipt-pdf-download' } = {}) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot download receipt.')
    return false
  }

  return downloadOrOpenPdf({
    urlForOpen: receiptPdfUrl(receiptId, { open: true }),
    fetchFn: () => paymentsAPI.getReceiptPdf(receiptId),
    filename: safeReceiptName(receiptId, receiptNo),
    toastId
  })
}

export function openStatementPdfForPrint (customerId, fromDate, toDate) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const url = statementPdfUrl(customerId, fromDate, toDate, { print: true, open: true })
  return openPdfUrlForPrint(url, {
    debounceKey: `print:statement:${customerId}:${fromDate}:${toDate}`,
    successMessage: isIOSDevice()
      ? 'Statement opened. Tap Share, then Print.'
      : 'Statement opened for printing.'
  })
}

export function openPendingBillsPdfForPrint (customerId, fromDate, toDate) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const url = pendingBillsPdfUrl(customerId, fromDate, toDate, { print: true, open: true })
  return openPdfUrlForPrint(url, {
    debounceKey: `print:pending:${customerId}:${fromDate}:${toDate}`,
    successMessage: isIOSDevice()
      ? 'Pending bills PDF opened. Tap Share, then Print.'
      : 'Pending bills PDF opened for printing.'
  })
}

export async function downloadPendingBillsPdf (customerId, fromDate, toDate, customerName, { toastId = 'pending-pdf-download' } = {}) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }

  return downloadOrOpenPdf({
    urlForOpen: pendingBillsPdfUrl(customerId, fromDate, toDate, { open: true }),
    fetchFn: () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    filename: safeStatementName(customerName, 'pending_bills'),
    toastId
  })
}

export async function downloadStatementPdf (customerId, fromDate, toDate, customerName, { toastId = 'statement-pdf-download' } = {}) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }

  return downloadOrOpenPdf({
    urlForOpen: statementPdfUrl(customerId, fromDate, toDate, { open: true }),
    fetchFn: () => customersAPI.getCustomerStatement(
      customerId,
      new Date(fromDate).toISOString(),
      new Date(toDate).toISOString()
    ),
    filename: safeStatementName(customerName, 'statement'),
    toastId
  })
}
