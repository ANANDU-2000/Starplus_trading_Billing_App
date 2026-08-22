import {
  needsBlobPdfFlow,
  savePdfToDevice,
  printPdfBlob,
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
 * Mobile/tablet print: Share sheet (print apps) → direct server URL → desktop print fallback.
 */
export async function mobilePrintPdf ({ blob, filename, directUrl }) {
  const typed = toPdfBlob(blob)

  if (typed && filename) {
    const file = new File([typed], filename, { type: 'application/pdf' })
    if (typeof navigator !== 'undefined' && navigator.canShare?.({ files: [file] })) {
      try {
        await navigator.share({ files: [file], title: filename })
        markPrintHintSeen()
        return { ok: true, method: 'share' }
      } catch (err) {
        if (err?.name === 'AbortError') return { ok: true, method: 'cancelled' }
      }
    }
  }

  if (directUrl && openPdfDirectUrl(directUrl)) {
    markPrintHintSeen()
    return { ok: true, method: 'tab' }
  }

  if (typed && !needsBlobPdfFlow()) {
    return printPdfBlob(typed)
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
    const result = await savePdfToDevice(typed, filename)
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
