import {
  needsBlobPdfFlow,
  savePdfToDevice,
  printPdfBlob,
  printPdfDirectUrl,
  openPdfDirectUrl
} from './blobDownload'
import { getPrintResultToast, markPrintHintSeen } from './pdfHints'

function toPdfBlob (blob) {
  if (!blob) return null
  return blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
}

/**
 * Mobile/tablet print: direct server URL print dialog → blob iframe print → open tab fallback.
 * Never uses Share sheet for Print PDF.
 */
export async function mobilePrintPdf ({ blob, filename, directUrl }) {
  if (directUrl) {
    const result = await printPdfDirectUrl(directUrl)
    if (result.ok) {
      markPrintHintSeen()
      return result
    }
  }

  const typed = toPdfBlob(blob)
  if (typed) {
    const blobResult = await printPdfBlob(typed)
    if (blobResult.ok) {
      markPrintHintSeen()
      return blobResult
    }
  }

  if (directUrl && openPdfDirectUrl(directUrl)) {
    markPrintHintSeen()
    return { ok: true, method: 'tab' }
  }

  return { ok: false, method: 'failed' }
}

export function mobileViewPdf ({ directUrl }) {
  if (!directUrl) return false
  return openPdfDirectUrl(directUrl)
}

export async function mobileDownloadPdf ({ blob, filename, directUrl }) {
  const typed = toPdfBlob(blob)
  if (typed) {
    const result = await savePdfToDevice(typed, filename, { directUrl })
    if (result !== 'tab' || !directUrl) return result
  }
  if (directUrl && openPdfDirectUrl(directUrl)) return 'tab'
  return 'failed'
}

export function toastPrintResult (result, toast) {
  const { type, message, duration } = getPrintResultToast(result?.method || 'failed')
  if (type === 'silent') return
  if (type === 'success') toast.success(message)
  else if (type === 'info') toast(message, { duration: duration || 6000, icon: 'ℹ️' })
  else toast.error(message)
}

export { needsBlobPdfFlow }
