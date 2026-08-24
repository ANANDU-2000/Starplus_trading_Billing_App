import toast from 'react-hot-toast'
import { salesAPI, paymentsAPI, customersAPI } from '../services'
import { usePdfDocumentStore } from '../stores/pdfDocumentStore'
import {
  isStandalonePwaMode as checkStandalonePwa,
  savePdfToDevice,
  printPdfBlob,
  instantPrintPdfUrl,
  needsBlobPdfFlow,
  openPdfDirectUrl
} from './blobDownload'
import { mobileViewPdf, toastPrintResult } from './pdfMobile'
import {
  getInvoicePdfUrl,
  getReceiptPdfUrl,
  getStatementPdfUrl,
  getPendingBillsPdfUrl
} from './pdfUrls'
import { getDownloadHintText, getPrintHintText } from './pdfHints'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'
import { getCachedInvoicePdf, setCachedInvoicePdf } from './pdfBlobCache'

const recentOpenByKey = new Map()
const DEBOUNCE_MS = 400

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

function markDebounce (key) {
  recentOpenByKey.set(key, Date.now())
}

function shouldDebounce (key) {
  const now = Date.now()
  const last = recentOpenByKey.get(key) || 0
  if (now - last < DEBOUNCE_MS) return true
  markDebounce(key)
  return false
}

function openPdfDocument ({ title, filename, fetchPdf, mode = 'view', directUrl = null }) {
  if (!fetchPdf && !directUrl) {
    toast.error('Cannot open PDF')
    return false
  }
  usePdfDocumentStore.getState().openPdfDocument({
    title,
    filename,
    fetchPdf,
    mode,
    directUrl
  })
  return true
}

async function fetchAndValidate (fetchPdf) {
  const raw = await fetchPdf()
  const data = raw instanceof Blob ? raw : new Blob([raw])
  const check = await validatePdfBlob(data)
  if (!check.ok) {
    throw new Error(check.message || 'Server did not return a valid PDF')
  }
  return new Blob([check.blob], { type: 'application/pdf' })
}

/**
 * Instant print — anonymous endpoints only (invoice / receipt AllowAnonymous).
 * Opens server PDF URL and triggers print immediately.
 */
function runInstantPrint ({ getDirectUrl, fetchPdf, filename }) {
  const directUrl = getDirectUrl?.({ print: true })
  if (!directUrl) {
    toast.error('Cannot print — missing PDF URL')
    return false
  }

  void instantPrintPdfUrl(directUrl).then(async (result) => {
    if (result.ok && result.method === 'dialog') return

    if (result.ok && result.method === 'tab') {
      toast(getPrintHintText(), { duration: 5000, icon: 'ℹ️' })
      return
    }

    try {
      const typed = await fetchAndValidate(fetchPdf)
      const blobResult = await printPdfBlob(typed)
      if (blobResult.ok) {
        toastPrintResult(blobResult, toast)
        return
      }
    } catch (err) {
      const msg = await parseApiErrorBlobMessage(err, 'Print failed')
      toast.error(msg)
      return
    }
    toast.error('Print failed — allow popups for this site')
  })

  return true
}

/**
 * Authenticated print — statement / pending bills / any [Authorize] PDF.
 * Never window.open(apiUrl) without working auth: fetch with Bearer, then print blob.
 * Fallback: withAuth direct URL (JwtBearer OnMessageReceived) if blob print fails.
 */
function runAuthenticatedPrint ({ fetchPdf, filename, debounceKey, getDirectUrl }) {
  if (debounceKey && shouldDebounce(debounceKey)) {
    toast('Opening print…', { duration: 1500 })
    return false
  }
  if (debounceKey) markDebounce(debounceKey)

  const toastId = toast.loading('Opening print…')
  void (async () => {
    try {
      const typed = await fetchAndValidate(fetchPdf)
      const result = await printPdfBlob(typed)
      if (result.ok) {
        toast.dismiss(toastId)
        toastPrintResult(result, toast)
        return
      }
      const directUrl = getDirectUrl?.({ print: true })
      if (directUrl) {
        const fallback = await instantPrintPdfUrl(directUrl)
        toast.dismiss(toastId)
        if (fallback.ok) {
          toastPrintResult(fallback, toast)
          return
        }
      }
      toast.dismiss(toastId)
      toast.error('Print failed — allow popups for this site')
    } catch (err) {
      toast.dismiss(toastId)
      const msg = await parseApiErrorBlobMessage(err, 'Print failed — login again if session expired')
      toast.error(msg)
    }
  })()

  return true
}

async function runPdfAction ({
  title,
  filename,
  fetchPdf,
  action,
  debounceKey,
  getDirectUrl,
  openModalOnFailure = true,
  /** When true, print/view must use axios Bearer (never raw API tab without auth). */
  requiresAuth = false
}) {
  if (action === 'print') {
    if (requiresAuth) {
      return runAuthenticatedPrint({ fetchPdf, filename, debounceKey })
    }
    if (debounceKey && shouldDebounce(debounceKey)) {
      toast('Opening print…', { duration: 1500 })
      return false
    }
    if (debounceKey) markDebounce(debounceKey)
    return runInstantPrint({ getDirectUrl, fetchPdf, filename })
  }

  if (debounceKey && shouldDebounce(debounceKey)) {
    toast('Please wait…', { duration: 1500 })
    return false
  }
  if (debounceKey) markDebounce(debounceKey)

  const directUrl = getDirectUrl?.({ print: false, open: action === 'view' }) || null
  const isMobile = needsBlobPdfFlow()

  if (action === 'view') {
    // Auth-required PDFs: open withAuth URL (works after JwtBearer query token) or modal
    if (requiresAuth) {
      if (directUrl && openPdfDirectUrl(directUrl)) {
        return true
      }
      return openPdfDocument({ title, filename, fetchPdf, mode: 'view', directUrl })
    }
    if (directUrl && openPdfDirectUrl(directUrl)) {
      return true
    }
    if (isMobile && directUrl && mobileViewPdf({ directUrl })) {
      return true
    }
    return openPdfDocument({ title, filename, fetchPdf, mode: 'view', directUrl })
  }

  const toastId = toast.loading('Preparing PDF…')
  try {
    const typed = await fetchAndValidate(fetchPdf)

    if (action === 'download') {
      const result = await savePdfToDevice(typed, filename, { directUrl: requiresAuth ? null : directUrl })
      toast.dismiss(toastId)
      if (result === 'cancelled') return true
      if (result === 'picker' || result === 'share') {
        toast.success('PDF saved — check Files or Downloads')
      } else if (result === 'download') {
        toast.success(getDownloadHintText())
      } else {
        toast(getDownloadHintText(), { duration: 5000, icon: 'ℹ️' })
      }
      return true
    }
  } catch (err) {
    toast.dismiss(toastId)
    const msg = await parseApiErrorBlobMessage(err, 'Failed to load PDF')
    toast.error(msg)
    if (openModalOnFailure && !isMobile) {
      openPdfDocument({ title, filename, fetchPdf, mode: action, directUrl })
    }
    return false
  }

  return false
}

/** Download or save a blob that was already fetched (reports pages). */
export async function saveValidatedPdfBlob (blob, filename, { directUrl } = {}) {
  const check = await validatePdfBlob(blob instanceof Blob ? blob : new Blob([blob]))
  if (!check.ok) {
    throw new Error(check.message || 'Invalid PDF')
  }
  const typed = new Blob([check.blob], { type: 'application/pdf' })
  const result = await savePdfToDevice(typed, filename, { directUrl })
  if (result === 'cancelled') return 'cancelled'
  if (result === 'picker' || result === 'share') {
    toast.success('PDF saved — check Files or Downloads')
  } else if (result === 'download') {
    toast.success(getDownloadHintText())
  } else {
    toast(getDownloadHintText(), { duration: 5000, icon: 'ℹ️' })
  }
  return result
}

async function fetchInvoicePdfValidated (saleId) {
  const cached = getCachedInvoicePdf(saleId)
  if (cached) return cached

  const typed = await fetchAndValidate(() => salesAPI.getInvoicePdf(saleId))
  setCachedInvoicePdf(saleId, typed)
  return typed
}

/** Prefetch after POS sale — optional background cache (print does not wait for this). */
export async function prefetchInvoicePdf (saleId) {
  if (!saleId) return { ok: false, error: 'No sale id' }
  try {
    await fetchInvoicePdfValidated(saleId)
    return { ok: true }
  } catch (err) {
    console.warn('[prefetchInvoicePdf]', saleId, err)
    const msg = err?.message || 'Failed to load invoice PDF'
    return { ok: false, error: msg }
  }
}

export async function loadCachedInvoicePdfUrl (saleId) {
  const blob = await fetchInvoicePdfValidated(saleId)
  return URL.createObjectURL(blob)
}

export { fetchInvoicePdfValidated, getCachedInvoicePdf, instantPrintPdfUrl }

function invoicePdfFetcher (saleId) {
  return () => fetchInvoicePdfValidated(saleId)
}

// --- Invoice (sale) ---

export function openInvoicePdfForPrint (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  return runInstantPrint({
    getDirectUrl: (opts) => getInvoicePdfUrl(saleId, opts),
    fetchPdf: invoicePdfFetcher(saleId),
    filename: safeInvoiceName(saleId, invoiceNo)
  })
}

export function openInvoicePdfForViewing (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  void runPdfAction({
    title: `Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    getDirectUrl: (opts) => getInvoicePdfUrl(saleId, opts),
    action: 'view'
  })
  return true
}

export function downloadInvoicePdf (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot download invoice.')
    return false
  }
  void runPdfAction({
    title: `Download — Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    getDirectUrl: (opts) => getInvoicePdfUrl(saleId, opts),
    action: 'download',
    debounceKey: `download:invoice:${saleId}`,
    openModalOnFailure: false
  })
  return true
}

// --- Payment receipt ---

export function openReceiptPdfForPrint (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot print receipt.')
    return false
  }
  return runInstantPrint({
    getDirectUrl: (opts) => getReceiptPdfUrl(receiptId, opts),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    filename: safeReceiptName(receiptId, receiptNo)
  })
}

export function openReceiptPdfForViewing (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot open receipt.')
    return false
  }
  void runPdfAction({
    title: `Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    getDirectUrl: (opts) => getReceiptPdfUrl(receiptId, opts),
    action: 'view'
  })
  return true
}

export function downloadReceiptPdf (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot download receipt.')
    return false
  }
  void runPdfAction({
    title: `Download — Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    getDirectUrl: (opts) => getReceiptPdfUrl(receiptId, opts),
    action: 'download',
    debounceKey: `download:receipt:${receiptId}`,
    openModalOnFailure: false
  })
  return true
}

// --- Customer statement / pending bills ---

export function openStatementPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const from = toDateOnly(fromDate)
  const to = toDateOnly(toDate)
  return runAuthenticatedPrint({
    fetchPdf: () => customersAPI.getCustomerStatement(customerId, from, to),
    filename: safeStatementName(customerName, 'statement'),
    debounceKey: `print:statement:${customerId}:${from}:${to}`,
    getDirectUrl: (opts) => getStatementPdfUrl(customerId, from, to, opts)
  })
}

export function openPendingBillsPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const from = toDateOnly(fromDate)
  const to = toDateOnly(toDate)
  return runAuthenticatedPrint({
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, from, to),
    filename: safeStatementName(customerName, 'pending_bills'),
    debounceKey: `print:pending:${customerId}:${from}:${to}`,
    getDirectUrl: (opts) => getPendingBillsPdfUrl(customerId, from, to, opts)
  })
}

export function downloadStatementPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const from = toDateOnly(fromDate)
  const to = toDateOnly(toDate)
  return runPdfAction({
    title: `Download Statement — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'statement'),
    fetchPdf: () => customersAPI.getCustomerStatement(customerId, from, to),
    getDirectUrl: (opts) => getStatementPdfUrl(customerId, from, to, opts),
    action: 'download',
    debounceKey: `download:statement:${customerId}:${from}:${to}`,
    openModalOnFailure: false,
    requiresAuth: true
  })
}

export function downloadPendingBillsPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  const from = toDateOnly(fromDate)
  const to = toDateOnly(toDate)
  return runPdfAction({
    title: `Download Pending Bills — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'pending_bills'),
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, from, to),
    getDirectUrl: (opts) => getPendingBillsPdfUrl(customerId, from, to, opts),
    action: 'download',
    debounceKey: `download:pending:${customerId}:${from}:${to}`,
    openModalOnFailure: false,
    requiresAuth: true
  })
}

export async function loadPdfBlobUrl (fetchFn) {
  const typed = await fetchAndValidate(fetchFn)
  return URL.createObjectURL(typed)
}

export { parseApiErrorBlobMessage }

export function isStandalonePwaMode () {
  return checkStandalonePwa()
}
