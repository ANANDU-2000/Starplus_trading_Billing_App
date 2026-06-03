import toast from 'react-hot-toast'
import { salesAPI, paymentsAPI, customersAPI } from '../services'
import { usePdfDocumentStore } from '../stores/pdfDocumentStore'
import { isStandalonePwaMode as checkStandalonePwa } from './blobDownload'
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

function shouldDebounce (key) {
  const now = Date.now()
  const last = recentOpenByKey.get(key) || 0
  if (now - last < DEBOUNCE_MS) return true
  recentOpenByKey.set(key, now)
  return false
}

function openPdfDocument ({ title, filename, fetchPdf, mode = 'view', debounceKey }) {
  if (debounceKey && shouldDebounce(debounceKey)) return false
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

async function fetchInvoicePdfValidated (saleId) {
  const cached = getCachedInvoicePdf(saleId)
  if (cached) return cached

  const raw = await salesAPI.getInvoicePdf(saleId)
  const data = raw instanceof Blob ? raw : new Blob([raw])
  const check = await validatePdfBlob(data)
  if (!check.ok) {
    throw new Error(check.message || 'Server did not return a valid PDF')
  }
  const typed = new Blob([check.blob], { type: 'application/pdf' })
  setCachedInvoicePdf(saleId, typed)
  return typed
}

/** Prefetch after POS sale — speeds up View/Print/Save on tablet */
export async function prefetchInvoicePdf (saleId) {
  if (!saleId) return false
  try {
    await fetchInvoicePdfValidated(saleId)
    return true
  } catch (err) {
    console.warn('[prefetchInvoicePdf]', saleId, err)
    return false
  }
}

function invoicePdfFetcher (saleId) {
  return () => fetchInvoicePdfValidated(saleId)
}

// --- Invoice (sale) ---

export function openInvoicePdfForPrint (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  return openPdfDocument({
    title: `Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    mode: 'print',
    debounceKey: `print:invoice:${saleId}`
  })
}

export function openInvoicePdfForViewing (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  return openPdfDocument({
    title: `Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    mode: 'view',
    debounceKey: `view:invoice:${saleId}`
  })
}

export function downloadInvoicePdf (saleId, invoiceNo) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot download invoice.')
    return false
  }
  return openPdfDocument({
    title: `Download — Invoice ${invoiceNo || saleId}`,
    filename: safeInvoiceName(saleId, invoiceNo),
    fetchPdf: invoicePdfFetcher(saleId),
    mode: 'download',
    debounceKey: `download:invoice:${saleId}`
  })
}

// --- Payment receipt ---

export function openReceiptPdfForPrint (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot print receipt.')
    return false
  }
  return openPdfDocument({
    title: `Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    mode: 'print',
    debounceKey: `print:receipt:${receiptId}`
  })
}

export function openReceiptPdfForViewing (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot open receipt.')
    return false
  }
  return openPdfDocument({
    title: `Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    mode: 'view',
    debounceKey: `view:receipt:${receiptId}`
  })
}

export function downloadReceiptPdf (receiptId, receiptNo) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot download receipt.')
    return false
  }
  return openPdfDocument({
    title: `Download — Receipt ${receiptNo || receiptId}`,
    filename: safeReceiptName(receiptId, receiptNo),
    fetchPdf: () => paymentsAPI.getReceiptPdf(receiptId),
    mode: 'download',
    debounceKey: `download:receipt:${receiptId}`
  })
}

// --- Customer statement / pending bills ---

export function openStatementPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfDocument({
    title: `Ledger Statement — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'statement'),
    fetchPdf: () => customersAPI.getCustomerStatement(
      customerId,
      new Date(fromDate).toISOString(),
      new Date(toDate).toISOString()
    ),
    mode: 'print',
    debounceKey: `print:statement:${customerId}:${fromDate}:${toDate}`
  })
}

export function openPendingBillsPdfForPrint (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfDocument({
    title: `Pending Bills — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'pending_bills'),
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    mode: 'print',
    debounceKey: `print:pending:${customerId}:${fromDate}:${toDate}`
  })
}

export function downloadStatementPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfDocument({
    title: `Download Statement — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'statement'),
    fetchPdf: () => customersAPI.getCustomerStatement(
      customerId,
      new Date(fromDate).toISOString(),
      new Date(toDate).toISOString()
    ),
    mode: 'download',
    debounceKey: `download:statement:${customerId}:${fromDate}:${toDate}`
  })
}

export function downloadPendingBillsPdf (customerId, fromDate, toDate, customerName) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfDocument({
    title: `Download Pending Bills — ${customerName || 'Customer'}`,
    filename: safeStatementName(customerName, 'pending_bills'),
    fetchPdf: () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    mode: 'download',
    debounceKey: `download:pending:${customerId}:${fromDate}:${toDate}`
  })
}

export async function loadPdfBlobUrl (fetchFn) {
  const raw = await fetchFn()
  const data = raw instanceof Blob ? raw : new Blob([raw])
  const check = await validatePdfBlob(data)
  if (!check.ok) throw new Error(check.message || 'Invalid PDF')
  return URL.createObjectURL(new Blob([check.blob], { type: 'application/pdf' }))
}

export { parseApiErrorBlobMessage }

export function isStandalonePwaMode () {
  return checkStandalonePwa()
}
