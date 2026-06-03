import toast from 'react-hot-toast'
import { salesAPI, paymentsAPI, customersAPI } from '../services'
import {
  triggerBlobDownload,
  openPdfBlobInViewer,
  shareOrSavePdfBlob,
  isIOSDevice,
  needsBlobPdfFlow
} from './blobDownload'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'

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
  if (now - last < DEBOUNCE_MS) {
    return true
  }
  recentOpenByKey.set(key, now)
  return false
}

async function fetchValidatedPdf (fetchFn) {
  const response = await fetchFn()
  const raw = response instanceof Blob ? response : new Blob([response])
  const check = await validatePdfBlob(raw)
  if (!check.ok) {
    throw new Error(check.message || 'Server did not return a valid PDF')
  }
  return new Blob([check.blob], { type: 'application/pdf' })
}

/**
 * Always use authenticated axios fetch + blob URL — reliable on tablet/PWA/Huawei
 * (window.open to API URL often shows HTML/JSON preview or wrong app handler).
 */
async function openPdfFromFetch (fetchFn, { debounceKey, toastId = 'pdf-open', forPrint = false } = {}) {
  if (debounceKey && shouldDebounce(debounceKey)) {
    return false
  }

  try {
    toast.loading(forPrint ? 'Preparing PDF for print...' : 'Opening PDF...', { id: toastId })
    const blob = await fetchValidatedPdf(fetchFn)
    const method = openPdfBlobInViewer(blob)
    toast.dismiss(toastId)

    if (method === 'new-tab') {
      toast.success(
        forPrint
          ? (isIOSDevice()
              ? 'PDF opened. Tap Share → Print or Save to Files.'
              : 'PDF opened. Use menu to Print or Save.')
          : (isIOSDevice()
              ? 'PDF opened. Tap Share to save or print.'
              : 'PDF opened in new tab.')
      )
      return true
    }

    toast('PDF opened in this tab. Use browser menu to print or save, then go back.', {
      icon: 'i',
      duration: 5500
    })
    return true
  } catch (error) {
    console.error('[pdf-open] failed:', error)
    toast.dismiss(toastId)
    const msg = await parseApiErrorBlobMessage(error, 'Failed to open PDF')
    toast.error(msg)
    return false
  }
}

async function downloadPdfFromFetch (fetchFn, filename, { toastId = 'pdf-download' } = {}) {
  try {
    toast.loading('Preparing PDF...', { id: toastId })
    const blob = await fetchValidatedPdf(fetchFn)
    toast.dismiss(toastId)

    if (needsBlobPdfFlow()) {
      const result = await shareOrSavePdfBlob(blob, filename)
      if (result === 'cancelled') return false
      if (result === 'share') {
        toast.success('Use Save to Files or Print from the share menu')
        return true
      }
      toast.success(
        isIOSDevice()
          ? 'PDF opened. Tap Share → Save to Files.'
          : 'PDF opened. Use ⋮ menu → Download or Print.'
      )
      return true
    }

    triggerBlobDownload(blob, filename)
    toast.success('Download started — check your downloads folder')
    return true
  } catch (error) {
    console.error('[pdf-download] failed:', error)
    toast.dismiss(toastId)
    const msg = await parseApiErrorBlobMessage(error, 'Failed to download PDF')
    toast.error(msg)
    return false
  }
}

/** Load PDF as blob URL for iframe preview (authenticated). */
export async function loadPdfBlobUrl (fetchFn) {
  const blob = await fetchValidatedPdf(fetchFn)
  return URL.createObjectURL(blob)
}

// --- Invoice (sale) ---

export async function openInvoicePdfForPrint (saleId) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  return openPdfFromFetch(
    () => salesAPI.getInvoicePdf(saleId),
    { debounceKey: `print:invoice:${saleId}`, forPrint: true }
  )
}

export async function openInvoicePdfForViewing (saleId) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  return openPdfFromFetch(
    () => salesAPI.getInvoicePdf(saleId),
    { debounceKey: `view:invoice:${saleId}`, toastId: 'pdf-view' }
  )
}

export async function downloadInvoicePdf (saleId, invoiceNo, { toastId = 'invoice-pdf-download' } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot download invoice.')
    return false
  }
  return downloadPdfFromFetch(
    () => salesAPI.getInvoicePdf(saleId),
    safeInvoiceName(saleId, invoiceNo),
    { toastId }
  )
}

// --- Payment receipt ---

export async function openReceiptPdfForPrint (receiptId) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot print receipt.')
    return false
  }
  return openPdfFromFetch(
    () => paymentsAPI.getReceiptPdf(receiptId),
    { debounceKey: `print:receipt:${receiptId}`, forPrint: true }
  )
}

export async function openReceiptPdfForViewing (receiptId) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot open receipt.')
    return false
  }
  return openPdfFromFetch(
    () => paymentsAPI.getReceiptPdf(receiptId),
    { debounceKey: `view:receipt:${receiptId}`, toastId: 'pdf-view' }
  )
}

export async function downloadReceiptPdf (receiptId, receiptNo, { toastId = 'receipt-pdf-download' } = {}) {
  if (!receiptId) {
    toast.error('Invalid receipt ID. Cannot download receipt.')
    return false
  }
  return downloadPdfFromFetch(
    () => paymentsAPI.getReceiptPdf(receiptId),
    safeReceiptName(receiptId, receiptNo),
    { toastId }
  )
}

// --- Customer statement / pending bills ---

export async function openStatementPdfForPrint (customerId, fromDate, toDate) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfFromFetch(
    () => customersAPI.getCustomerStatement(
      customerId,
      new Date(fromDate).toISOString(),
      new Date(toDate).toISOString()
    ),
    { debounceKey: `print:statement:${customerId}:${fromDate}:${toDate}`, forPrint: true }
  )
}

export async function openPendingBillsPdfForPrint (customerId, fromDate, toDate) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return openPdfFromFetch(
    () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    { debounceKey: `print:pending:${customerId}:${fromDate}:${toDate}`, forPrint: true }
  )
}

export async function downloadStatementPdf (customerId, fromDate, toDate, customerName, { toastId = 'statement-pdf-download' } = {}) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return downloadPdfFromFetch(
    () => customersAPI.getCustomerStatement(
      customerId,
      new Date(fromDate).toISOString(),
      new Date(toDate).toISOString()
    ),
    safeStatementName(customerName, 'statement'),
    { toastId }
  )
}

export async function downloadPendingBillsPdf (customerId, fromDate, toDate, customerName, { toastId = 'pending-pdf-download' } = {}) {
  if (!customerId) {
    toast.error('Please select a customer first.')
    return false
  }
  return downloadPdfFromFetch(
    () => customersAPI.getCustomerPendingBillsPdf(customerId, fromDate, toDate),
    safeStatementName(customerName, 'pending_bills'),
    { toastId }
  )
}

// Legacy URL builders kept for debugging only — do not use for open/print on devices
export function invoicePdfUrl () {
  console.warn('invoicePdfUrl: use openInvoicePdfForPrint instead')
  return ''
}

export function receiptPdfUrl () {
  console.warn('receiptPdfUrl: use openReceiptPdfForPrint instead')
  return ''
}

export function isStandalonePwaMode () {
  return needsBlobPdfFlow()
}
