/**
 * Programmatic download from a Blob. Revokes the object URL after a delay so the browser
 * can start the save (immediate revoke often cancels the download).
 */
export function triggerBlobDownload (blob, filename, { revokeDelayMs = 60_000 } = {}) {
  if (!blob || blob.size === 0) {
    throw new Error('Empty file')
  }
  const typed = blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
  const url = URL.createObjectURL(typed)
  const a = document.createElement('a')
  a.href = url
  a.download = filename
  a.rel = 'noopener'
  document.body.appendChild(a)
  a.click()
  document.body.removeChild(a)
  setTimeout(() => URL.revokeObjectURL(url), revokeDelayMs)
}

/**
 * Open PDF blob in a new tab (works on tablet/PWA when direct API URL fails).
 */
export function openPdfBlobInViewer (blob, { revokeDelayMs = 120_000 } = {}) {
  if (!blob || blob.size === 0) return false
  const typed = blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
  const url = URL.createObjectURL(typed)
  const w = window.open(url, '_blank', 'noopener,noreferrer')
  if (w) {
    setTimeout(() => URL.revokeObjectURL(url), revokeDelayMs)
    return 'new-tab'
  }
  window.location.assign(url)
  setTimeout(() => URL.revokeObjectURL(url), revokeDelayMs)
  return 'same-tab'
}

/**
 * iOS Safari (and some Android browsers) often ignore `download` on `<a>` for blobs.
 * Opens the blob in a new tab so the user can Share → Save / Print to PDF.
 */
export function tryOpenBlobInNewTab (blob, { revokeDelayMs = 120_000 } = {}) {
  if (!blob || blob.size === 0) return false
  const result = openPdfBlobInViewer(blob, { revokeDelayMs })
  return result === 'new-tab'
}

/**
 * Tablet/PWA: save via system share sheet (Files/Downloads) when supported.
 */
export async function shareOrSavePdfBlob (blob, filename) {
  const typed = blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
  const file = new File([typed], filename, { type: 'application/pdf' })

  if (typeof navigator !== 'undefined' && navigator.canShare?.({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: filename })
      return 'share'
    } catch (err) {
      if (err?.name === 'AbortError') return 'cancelled'
    }
  }

  const opened = openPdfBlobInViewer(typed)
  if (opened) return opened

  triggerBlobDownload(typed, filename)
  return 'download'
}

export function isIOSDevice () {
  if (typeof navigator === 'undefined') return false
  if (/iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream) return true
  if (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) return true
  return false
}

export function isLikelyMobileBrowser () {
  if (typeof navigator === 'undefined') return false
  if (isIOSDevice()) return true
  return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini|Huawei|HONOR/i.test(navigator.userAgent)
}

export function isStandalonePwaMode () {
  try {
    return window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true
  } catch {
    return false
  }
}

export function needsBlobPdfFlow () {
  return isLikelyMobileBrowser() || isStandalonePwaMode()
}
