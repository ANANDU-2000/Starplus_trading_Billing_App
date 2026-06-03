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

export function tryOpenBlobInNewTab (blob, { revokeDelayMs = 120_000 } = {}) {
  if (!blob || blob.size === 0) return false
  const result = openPdfBlobInViewer(blob, { revokeDelayMs })
  return result === 'new-tab'
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

export function isTouchOrTabletDevice () {
  if (typeof navigator === 'undefined') return false
  try {
    if (isLikelyMobileBrowser() || isStandalonePwaMode()) return true
    if (navigator.maxTouchPoints > 0) return true
    if (window.matchMedia('(pointer: coarse)').matches) return true
  } catch {
    /* ignore */
  }
  return false
}

export function needsBlobPdfFlow () {
  return isTouchOrTabletDevice()
}

function toPdfBlob (blob) {
  return blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
}

/**
 * Save PDF on Honor/Android/PWA: Share → File picker → open tab fallback.
 * @returns {'share'|'picker'|'tab'|'download'|'cancelled'}
 */
export async function savePdfToDevice (blob, filename) {
  const typed = toPdfBlob(blob)
  const file = new File([typed], filename, { type: 'application/pdf' })

  if (typeof window.showSaveFilePicker === 'function') {
    try {
      const handle = await window.showSaveFilePicker({
        suggestedName: filename,
        types: [{
          description: 'PDF',
          accept: { 'application/pdf': ['.pdf'] }
        }]
      })
      const writable = await handle.createWritable()
      await writable.write(typed)
      await writable.close()
      return 'picker'
    } catch (err) {
      if (err?.name === 'AbortError') return 'cancelled'
    }
  }

  if (typeof navigator !== 'undefined' && navigator.canShare?.({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: filename })
      return 'share'
    } catch (err) {
      if (err?.name === 'AbortError') return 'cancelled'
    }
  }

  if (needsBlobPdfFlow()) {
    openPdfBlobInViewer(typed)
    return 'tab'
  }

  triggerBlobDownload(typed, filename)
  return 'download'
}

export async function shareOrSavePdfBlob (blob, filename) {
  return savePdfToDevice(blob, filename)
}

/**
 * Print the real PDF (not the HTML app page).
 * Prefer the visible preview iframe; otherwise open PDF in a new tab and print there.
 */
export function printPdfBlob (blob, { previewIframe = null } = {}) {
  return new Promise((resolve) => {
    if (!blob || blob.size === 0) {
      resolve(false)
      return
    }

    const typed = toPdfBlob(blob)
    let printed = false

    const tryPreviewIframe = () => {
      if (!previewIframe?.contentWindow) return false
      try {
        previewIframe.contentWindow.focus()
        previewIframe.contentWindow.print()
        printed = true
        resolve(true)
        return true
      } catch {
        return false
      }
    }

    if (tryPreviewIframe()) return

    const url = URL.createObjectURL(typed)
    const printWin = window.open(url, '_blank', 'noopener,noreferrer')

    if (!printWin) {
      URL.revokeObjectURL(url)
      resolve(false)
      return
    }

    const runPrint = () => {
      if (printed) return
      try {
        printWin.focus()
        printWin.print()
        printed = true
        resolve(true)
      } catch {
        resolve(false)
      }
    }

    printWin.addEventListener('load', () => {
      setTimeout(runPrint, 600)
    })

    setTimeout(() => {
      if (!printed) runPrint()
      setTimeout(() => URL.revokeObjectURL(url), 120_000)
    }, 2500)
  })
}
