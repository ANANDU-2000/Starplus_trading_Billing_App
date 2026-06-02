import toast from 'react-hot-toast'
import { salesAPI } from '../services'
import { triggerBlobDownload, isIOSDevice, isLikelyMobileBrowser } from './blobDownload'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'
const recentOpenBySale = new Map()

function safeInvoiceName (saleId, invoiceNo) {
  return `INV-${String(invoiceNo || saleId || 'invoice').replace(/[^\w.-]+/g, '_')}.pdf`
}

function isStandalonePwaMode () {
  try {
    return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
  } catch {
    return false
  }
}

export function invoicePdfUrl (saleId, { print = false, open = false, format, width } = {}) {
  const params = new URLSearchParams()
  if (print) params.set('print', '1')
  if (open) params.set('open', '1')
  if (format) params.set('format', format)
  if (width) params.set('width', width)
  params.set('_', Date.now().toString())
  return `${API_BASE_URL}/sales/${saleId}/pdf?${params.toString()}`
}

function openPdfWithFallback (url, { forPrint = false } = {}) {
  const inPwa = isStandalonePwaMode()
  console.debug(`[invoice-pdf] open attempt: forPrint=${forPrint}, pwa=${inPwa}, url=${url}`)

  const popup = window.open(url, '_blank', 'noopener,noreferrer')
  if (popup) {
    console.debug('[invoice-pdf] open strategy: new-tab')
    return 'new-tab'
  }

  // Installed PWA/webview can block new tabs. Same-tab is more reliable there.
  window.location.assign(url)
  console.debug('[invoice-pdf] open strategy: same-tab-fallback')
  return 'same-tab'
}

export function openInvoicePdfForPrint (saleId, { format, width } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }
  const now = Date.now()
  const key = `print:${saleId}`
  const last = recentOpenBySale.get(key) || 0
  if (now - last < 700) {
    console.debug(`[invoice-pdf] ignored duplicate print click for sale ${saleId}`)
    return false
  }
  recentOpenBySale.set(key, now)

  const printUrl = invoicePdfUrl(saleId, { print: true, open: true, format, width })
  const method = openPdfWithFallback(printUrl, { forPrint: true })
  if (method === 'new-tab') {
    toast.success(
      isIOSDevice()
        ? 'Invoice opened. Tap Share, then Print.'
        : 'Invoice opened for printing.'
    )
    return true
  }

  toast('Opened invoice in this tab for printing. Use browser print, then go back to app.', { icon: 'i', duration: 5500 })
  return true
}

export function openInvoicePdfForViewing (saleId) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }
  const now = Date.now()
  const key = `view:${saleId}`
  const last = recentOpenBySale.get(key) || 0
  if (now - last < 700) {
    console.debug(`[invoice-pdf] ignored duplicate view click for sale ${saleId}`)
    return false
  }
  recentOpenBySale.set(key, now)

  const url = invoicePdfUrl(saleId, { open: true })
  const method = openPdfWithFallback(url, { forPrint: false })
  if (method === 'same-tab') {
    toast('Opened invoice in this tab. Use browser menu to print or save, then go back.', { icon: 'i', duration: 5500 })
  }
  return true
}

export async function downloadInvoicePdf (saleId, invoiceNo, { toastId = 'invoice-pdf-download' } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot download invoice.')
    return false
  }

  if (isLikelyMobileBrowser()) {
    openInvoicePdfForViewing(saleId)
    toast.success(
      isIOSDevice()
        ? 'PDF opened. Tap Share to Save to Files or Print.'
        : 'PDF opened. Use the browser menu to download or print.'
    )
    return true
  }

  try {
    toast.loading('Preparing PDF...', { id: toastId })
    const response = await salesAPI.getInvoicePdf(saleId)
    const raw = response instanceof Blob ? response : new Blob([response], { type: 'application/pdf' })
    const check = await validatePdfBlob(raw)
    if (!check.ok) {
      toast.error(check.message, { id: toastId })
      return false
    }

    triggerBlobDownload(check.blob, safeInvoiceName(saleId, invoiceNo))
    toast.success('Download started - check your downloads folder', { id: toastId })
    return true
  } catch (error) {
    console.error('Failed to download invoice PDF:', error)
    const msg = await parseApiErrorBlobMessage(error, 'Failed to download PDF')
    toast.error(msg, { id: toastId })
    return false
  }
}
