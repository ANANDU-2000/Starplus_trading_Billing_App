const MOBILE_REVOKE_MS = 180_000
const DESKTOP_REVOKE_MS = 120_000

function blobRevokeDelay () {
  return needsBlobPdfFlow() ? MOBILE_REVOKE_MS : DESKTOP_REVOKE_MS
}

/**
 * Programmatic download from a Blob. Revokes the object URL after a delay so the browser
 * can start the save (immediate revoke often cancels the download).
 */
export function triggerBlobDownload (blob, filename, { revokeDelayMs } = {}) {
  if (!blob || blob.size === 0) {
    throw new Error('Empty file')
  }
  const delay = revokeDelayMs ?? blobRevokeDelay()
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
  setTimeout(() => URL.revokeObjectURL(url), delay)
}

/**
 * Open PDF blob in a new tab (works on tablet/PWA when direct API URL fails).
 */
export function openPdfBlobInViewer (blob, { revokeDelayMs } = {}) {
  if (!blob || blob.size === 0) return false
  const delay = revokeDelayMs ?? blobRevokeDelay()
  const typed = blob.type === 'application/pdf'
    ? blob
    : new Blob([blob], { type: 'application/pdf' })
  const url = URL.createObjectURL(typed)
  const w = window.open(url, '_blank', 'noopener,noreferrer')
  if (w) {
    setTimeout(() => URL.revokeObjectURL(url), delay)
    return 'new-tab'
  }
  window.location.assign(url)
  setTimeout(() => URL.revokeObjectURL(url), delay)
  return 'same-tab'
}

/**
 * iOS Safari (and some Android browsers) often ignore `download` on `<a>` for HTML blobs.
 * Opens the blob in a new tab so the user can Share → Save / Print to PDF.
 */
export function tryOpenBlobInNewTab (blob, { revokeDelayMs } = {}) {
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
 * Wait until an iframe has loaded its document (blob PDF viewer).
 */
export function waitForIframeReady (iframe, { timeoutMs = 8000 } = {}) {
  return new Promise((resolve) => {
    if (!iframe) {
      resolve(false)
      return
    }

    const done = () => {
      setTimeout(() => resolve(true), 300)
    }

    try {
      const doc = iframe.contentDocument
      if (doc?.readyState === 'complete') {
        done()
        return
      }
    } catch {
      /* cross-origin — rely on load event */
    }

    const onLoad = () => {
      iframe.removeEventListener('load', onLoad)
      done()
    }
    iframe.addEventListener('load', onLoad)

    setTimeout(() => {
      iframe.removeEventListener('load', onLoad)
      resolve(false)
    }, timeoutMs)
  })
}

function tryPrintIframe (iframe) {
  if (!iframe?.contentWindow) return false
  try {
    iframe.contentWindow.focus()
    iframe.contentWindow.print()
    return true
  } catch {
    return false
  }
}

/**
 * Print via a hidden off-screen iframe (no popup / user-gesture required).
 */
function printViaHiddenIframe (blob) {
  return new Promise((resolve) => {
    const typed = toPdfBlob(blob)
    const url = URL.createObjectURL(typed)
    const iframe = document.createElement('iframe')
    iframe.style.position = 'fixed'
    iframe.style.right = '0'
    iframe.style.bottom = '0'
    iframe.style.width = '0'
    iframe.style.height = '0'
    iframe.style.border = 'none'
    iframe.setAttribute('aria-hidden', 'true')

    let finished = false
    const cleanup = () => {
      setTimeout(() => {
        iframe.remove()
        URL.revokeObjectURL(url)
      }, blobRevokeDelay())
    }

    const finish = (ok) => {
      if (finished) return
      finished = true
      cleanup()
      resolve(ok)
    }

    iframe.onload = () => {
      setTimeout(() => {
        finish(tryPrintIframe(iframe))
      }, 300)
    }

    iframe.onerror = () => finish(false)
    document.body.appendChild(iframe)
    iframe.src = url

    setTimeout(() => {
      if (!finished) finish(tryPrintIframe(iframe))
    }, 5000)
  })
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
 * Uses preview iframe when ready, else hidden iframe, else new tab on mobile.
 */
export async function printPdfBlobWhenReady (blob, previewIframe) {
  if (!blob || blob.size === 0) return false

  if (previewIframe) {
    const ready = await waitForIframeReady(previewIframe)
    if (ready && tryPrintIframe(previewIframe)) return true
  }

  return printPdfBlob(blob, { previewIframe: null })
}

/**
 * Print the real PDF (not the HTML app page).
 */
export function printPdfBlob (blob, { previewIframe = null } = {}) {
  return new Promise((resolve) => {
    if (!blob || blob.size === 0) {
      resolve(false)
      return
    }

    const typed = toPdfBlob(blob)
    let printed = false

    const finish = (ok) => {
      if (printed) return
      printed = true
      resolve(ok)
    }

    if (previewIframe?.contentWindow) {
      waitForIframeReady(previewIframe).then((ready) => {
        if (ready && tryPrintIframe(previewIframe)) {
          finish(true)
          return
        }
        printViaHiddenIframe(typed).then((ok) => {
          if (ok) {
            finish(true)
            return
          }
          if (needsBlobPdfFlow()) {
            openPdfBlobInViewer(typed)
            finish(true)
            return
          }
          tryWindowPrint(typed, finish)
        })
      })
      return
    }

    printViaHiddenIframe(typed).then((ok) => {
      if (ok) {
        finish(true)
        return
      }
      if (needsBlobPdfFlow()) {
        openPdfBlobInViewer(typed)
        finish(true)
        return
      }
      tryWindowPrint(typed, finish)
    })
  })
}

function tryWindowPrint (typed, finish) {
  const url = URL.createObjectURL(typed)
  const printWin = window.open(url, '_blank', 'noopener,noreferrer')

  if (!printWin) {
    URL.revokeObjectURL(url)
    finish(false)
    return
  }

  const runPrint = () => {
    try {
      printWin.focus()
      printWin.print()
      finish(true)
    } catch {
      finish(false)
    }
  }

  printWin.addEventListener('load', () => {
    setTimeout(runPrint, 600)
  })

  setTimeout(() => {
    if (!printWin.closed) runPrint()
    setTimeout(() => URL.revokeObjectURL(url), blobRevokeDelay())
  }, 2500)
}
