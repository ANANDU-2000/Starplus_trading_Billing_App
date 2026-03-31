/**
 * Programmatic download from a Blob. Revokes the object URL after a delay so the browser
 * can start the save (immediate revoke often cancels the download).
 */
export function triggerBlobDownload (blob, filename, { revokeDelayMs = 60_000 } = {}) {
  if (!blob || blob.size === 0) {
    throw new Error('Empty file')
  }
  const url = URL.createObjectURL(blob)
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
 * iOS Safari (and some Android browsers) often ignore `download` on `<a>` for HTML blobs.
 * Opens the blob in a new tab so the user can Share → Save / Print to PDF.
 */
export function tryOpenBlobInNewTab (blob, { revokeDelayMs = 60_000 } = {}) {
  if (!blob || blob.size === 0) return false
  const url = URL.createObjectURL(blob)
  const w = window.open(url, '_blank', 'noopener,noreferrer')
  if (w) {
    setTimeout(() => URL.revokeObjectURL(url), revokeDelayMs)
    return true
  }
  URL.revokeObjectURL(url)
  return false
}

export function isIOSDevice () {
  if (typeof navigator === 'undefined') return false
  if (/iPad|iPhone|iPod/.test(navigator.userAgent) && !window.MSStream) return true
  // iPadOS 13+ often reports as Mac with touch
  if (navigator.platform === 'MacIntel' && navigator.maxTouchPoints > 1) return true
  return false
}

export function isLikelyMobileBrowser () {
  if (typeof navigator === 'undefined') return false
  return /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent)
}
