import toast from 'react-hot-toast'
import { salesAPI, paymentsAPI, customersAPI } from '../services'
import { usePdfDocumentStore } from '../stores/pdfDocumentStore'
import {
  isStandalonePwaMode as checkStandalonePwa,
  savePdfToDevice,
  printPdfBlob,
  needsBlobPdfFlow
} from './blobDownload'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'
import { getCachedInvoicePdf, setCachedInvoicePdf } from './pdfBlobCache'

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

function openPdfDocument ({ title, filename, fetchPdf, mode = 'view' }) {
  if (!fetchPdf) {
    toast.error('Cannot open PDF')
    return false
  }
  usePdfDocumentStore.getState().openPdfDocument({
    title,
    filename,
    fetchPdf,
    mode
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
 * Shared PDF action: view opens modal; print/download run directly (one tap).
 */
async function runPdfAction ({
  title,
  filename,
  fetchPdf,
  action,
  debounceKey,
  openModalOnFailure = true
}) {
  if (debounceKey && shouldDebounce(debounceKey)) {
    toast('Please wait — PDF is opening', { duration: 2000 })
    return false
  }
  if (debounceKey) markDebounce(debounceKey)

  if (action === 'view') {
    return openPdfDocument({ title, filename, fetchPdf, mode: 'view' })
  }

  const toastId = toast.loading('Preparing PDF…')
  try {
    const typed = await fetchAndValidate(fetchPdf)

    if (action === 'print') {
      const ok = await printPdfBlob(typed)
      toast.dismiss(toastId)
      if (ok) {
        if (needsBlobPdfFlow()) {
          toast('PDF opened — use ⋮ → Print in the PDF tab', { duration: 6000 })
        } else {
          toast.success('Print dialog opened')
        }
        return true
      }
      toast.error('Could not print directly — opening PDF viewer')
      if (openModalOnFailure) {
        openPdfDocument({
          title,
          filename,
          fetchPdf: () => Promise.resolve(typed),
          mode: 'print'
        })
      }
      return false
    }

    if (action === 'download') {
      const result = await savePdfToDevice(typed, filename)
      toast.dismiss(toastId)
      if (result === 'cancelled') return true
      if (result === 'picker' || result === 'share') {
        toast.success('PDF saved — check Files or Downloads')
      } else if (result === 'download') {
        toast.success('PDF saved to downloads')
      } else {
        toast('PDF opened — Share → Save to Files', { duration: 6000 })
      }
      return true
    }
  } catch (err) {
    toast.dismiss(toastId)
    const msg = await parseApiErrorBlobMessage(err, 'Failed to load PDF')
    toast.error(msg)
    if (openModalOnFailure && (action === 'print' || action === 'download')) {
      openPdfDocument({ title, filename, fetchPdf, mode: action })
    }
    return false
  }

  return false
}

/** Download or save a blob that was already fetched (reports pages). */
export async function saveValidatedPdfBlob (blob, filename) {
  const check = await validatePdfBlob(blob instanceof Blob ? blob : new Blob([blob]))
  if (!check.ok) {
    throw new Error(check.message || 'Invalid PDF')
  }
  const typed = new Blob([check.blob], { type: 'application/pdf' })
  const result = await savePdfToDevice(typed, filename)
  if (result === 'cancelled') return 'cancelled'
  if (result === 'picker' || result === 'share') {
    toast.success('PDF saved — check Files or Downloads')
  } else if (result === 'download') {
    toast.success('PDF saved to downloads')
  } else {
    toast('PDF opened — Share → Save to Files', { duration: 6000 })
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

/** Prefetch after POS sale — speeds up View/Print/Save on tablet */
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

export { fetchInvoicePdfValidated, getCachedInvoicePdf }

function invoicePdfFetcher (saleId) {
  return () => fetchInvoicePdfValidated(saleId)
}

// --- Invoice (sale) ---

export function openInvoicePdfForPrint (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  void runPdfAction({
    title: `Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    action: 'print',
    debounceKey: `print:invoice:${saleId}`
  })
  return true
}

export function openInvoicePdfForViewing (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  if (shouldDebounce(`view:invoice:${saleId}`)) {
    toast('Please wait — PDF is opening', { duration: 2000 })
    return false
  }
  markDebounce(`view:invoice:${saleId}`)
  return openPdfDocument({
    title: `Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    mode: 'view'
  })
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
    action: 'download',
    debounceKey: `download:invoice:${saleId}`
  })
  return true
}

// --- Payment receipt ---

export function openReceiptPdfForPrint (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot print receipt.')
    return false
  }
  void runPdfAction({
    title: `Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    action: 'print',
    debounceKey: `print:receipt:${receiptId}`
  })
  return true
}

export function openReceiptPdfForViewing (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot open receipt.')
    return false
  }
  if (shouldDebounce(`view:receipt:${receiptId}`)) {
    toast('Please wait — PDF is opening', { duration: 2000 })
    return false
  }
  markDebounce(`view:receipt:${receiptId}`)
  return openPdfDocument({
    title: `Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    mode: 'view'
  })
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
    action: 'download',
    debounceKey: `download:receipt:${receiptId}`
  })
  return true
}

// --- Customer statement / pending bills ---

export function openStatementPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  void runPdfAction({
    title: `Ledger Statement — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'statement'),
    fetchPdf: () => customersAPI.getCustomerStatement(customerId, fromDate, toDate),
    action: 'print',
    debounceKey: `print:statement:${customerId}:${fromDate}:${toDate}`
  })
  return true
}

export function openPendingBillsPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  void runPdfAction({
    title: `Pending Bills — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'pending_bills'),
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    action: 'print',
    debounceKey: `print:pending:${customerId}:${fromDate}:${toDate}`
  })
  return true
}

export function downloadStatementPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return runPdfAction({
    title: `Download Statement — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'statement'),
    fetchPdf: () => customersAPI.getCustomerStatement(customerId, fromDate, toDate),
    action: 'download',
    debounceKey: `download:statement:${customerId}:${fromDate}:${toDate}`
  })
}

export function downloadPendingBillsPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return runPdfAction({
    title: `Download Pending Bills — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'pending_bills'),
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    action: 'download',
    debounceKey: `download:pending:${customerId}:${fromDate}:${toDate}`
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
