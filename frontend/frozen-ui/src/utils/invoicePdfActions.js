import toast from 'react-hot-toast'
import { salesAPI } from '../services'
import { triggerBlobDownload, isIOSDevice, isLikelyMobileBrowser } from './blobDownload'
import { parseApiErrorBlobMessage, validatePdfBlob } from './pdfBlob'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

function safeInvoiceName (saleId, invoiceNo) {
  return `INV-${String(invoiceNo || saleId || 'invoice').replace(/[^\w.-]+/g, '_')}.pdf`
}

export function invoicePdfUrl (saleId, { print = false, format, width } = {}) {
  const params = new URLSearchParams()
  if (print) params.set('print', '1')
  if (format) params.set('format', format)
  if (width) params.set('width', width)
  params.set('_', Date.now().toString())
  return `${API_BASE_URL}/sales/${saleId}/pdf?${params.toString()}`
}

export function openInvoicePdfForPrint (saleId, { format, width } = {}) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot print invoice.')
    return false
  }

  const printUrl = invoicePdfUrl(saleId, { print: true, format, width })
  const printWindow = window.open(printUrl, '_blank', 'noopener,noreferrer')
  if (printWindow) {
    toast.success(
      isIOSDevice()
        ? 'Invoice opened. Tap Share, then Print.'
        : 'Invoice opened for printing.'
    )
    return true
  }

  window.location.assign(printUrl)
  toast('Pop-up blocked. Opened invoice in this tab for printing.', { icon: 'i', duration: 5000 })
  return true
}

export function openInvoicePdfForViewing (saleId) {
  if (!saleId) {
    toast.error('Invalid sale ID. Cannot open invoice.')
    return false
  }

  const url = invoicePdfUrl(saleId, { print: true })
  const popup = window.open(url, '_blank', 'noopener,noreferrer')
  if (!popup) {
    window.location.assign(url)
    toast('Pop-up blocked. Opened invoice in this tab.', { icon: 'i', duration: 4000 })
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
