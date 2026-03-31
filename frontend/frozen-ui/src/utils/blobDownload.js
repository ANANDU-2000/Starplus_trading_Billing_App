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
