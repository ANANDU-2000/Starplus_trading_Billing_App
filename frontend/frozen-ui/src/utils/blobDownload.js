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
 * Print via hidden iframe loading a server PDF URL (works on many Android tablets).
 */
function printViaHiddenIframeUrl (url) {
  return new Promise((resolve) => {
    if (!url) {
      resolve(false)
      return
    }

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
      setTimeout(() => iframe.remove(), 60_000)
    }

    const finish = (ok) => {
      if (finished) return
      finished = true
      cleanup()
      resolve(ok)
    }

    iframe.onload = () => {
      setTimeout(() => finish(tryPrintIframe(iframe)), 500)
    }
    iframe.onerror = () => finish(false)
    document.body.appendChild(iframe)
    iframe.src = url

    setTimeout(() => {
      if (!finished) finish(tryPrintIframe(iframe))
    }, 6000)
  })
}

/**
 * Open server PDF URL in new tab and trigger print (must not use noopener — need window ref).
 */
function tryWindowPrintUrl (url) {
  return new Promise((resolve) => {
    if (!url) {
      resolve(false)
      return
    }

    let printWin
    try {
      printWin = window.open(url, '_blank')
    } catch {
      resolve(false)
      return
    }

    if (!printWin) {
      resolve(false)
      return
    }

    let finished = false
    const finish = (ok) => {
      if (finished) return
      finished = true
      resolve(ok)
    }

    const runPrint = () => {
      try {
        printWin.focus()
        printWin.print()
        finish(true)
      } catch {
        if (!finished) finish(false)
      }
    }

    setTimeout(runPrint, 1200)
    setTimeout(runPrint, 2800)
  })
}

/**
 * Print a server-generated PDF URL directly — primary path for tablet/mobile.
 * @returns {Promise<{ ok: boolean, method: 'dialog'|'tab'|'failed' }>}
 */
export async function printPdfDirectUrl (url) {
  if (!url) return { ok: false, method: 'failed' }

  const iframeOk = await printViaHiddenIframeUrl(url)
  if (iframeOk) return { ok: true, method: 'dialog' }

  const winOk = await tryWindowPrintUrl(url)
  if (winOk) return { ok: true, method: 'dialog' }

  // Last resort: open tab so user can print from Chrome PDF viewer
  const opened = openPdfDirectUrl(url)
  return opened ? { ok: true, method: 'tab' } : { ok: false, method: 'failed' }
}

/**
 * Open a direct HTTPS PDF URL (server-generated). Works reliably on Android Chrome.
 */
export function openPdfDirectUrl (url) {
  if (!url) return false
  try {
    const w = window.open(url, '_blank', 'noopener,noreferrer')
    if (w) return true
    window.location.assign(url)
    return true
  } catch {
    return false
  }
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
 * Save PDF on tablet: direct download / open URL first; Share only as last resort.
 * @returns {'share'|'picker'|'tab'|'download'|'cancelled'}
 */
export async function savePdfToDevice (blob, filename, { directUrl } = {}) {
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

  if (!needsBlobPdfFlow()) {
    triggerBlobDownload(typed, filename)
    return 'download'
  }

  if (directUrl && openPdfDirectUrl(directUrl)) {
    return 'tab'
  }

  try {
    triggerBlobDownload(typed, filename)
    return 'download'
  } catch {
    /* fall through */
  }

  if (typeof navigator !== 'undefined' && navigator.canShare?.({ files: [file] })) {
    try {
      await navigator.share({ files: [file], title: filename })
      return 'share'
    } catch (err) {
      if (err?.name === 'AbortError') return 'cancelled'
    }
  }

  openPdfBlobInViewer(typed)
  return 'tab'
}

export async function shareOrSavePdfBlob (blob, filename) {
  return savePdfToDevice(blob, filename)
}

/** @returns {Promise<{ ok: boolean, method: 'dialog'|'tab'|'failed'|'cancelled' }>} */
export async function printPdfBlobWhenReady (blob, previewIframe, directUrl = null) {
  if (needsBlobPdfFlow()) {
    if (directUrl) return printPdfDirectUrl(directUrl)
    if (blob?.size) return printPdfBlob(blob, { previewIframe: null })
    return { ok: false, method: 'failed' }
  }

  if (!blob || blob.size === 0) return { ok: false, method: 'failed' }

  if (previewIframe) {
    const ready = await waitForIframeReady(previewIframe)
    if (ready && tryPrintIframe(previewIframe)) {
      return { ok: true, method: 'dialog' }
    }
  }

  return printPdfBlob(blob, { previewIframe: null })
}

/**
 * Print the real PDF (desktop). Mobile should use pdfMobile.mobilePrintPdf with direct URLs.
 * @returns {Promise<{ ok: boolean, method: 'dialog'|'tab'|'failed' }>}
 */
export function printPdfBlob (blob, { previewIframe = null } = {}) {
  return new Promise((resolve) => {
    if (!blob || blob.size === 0) {
      resolve({ ok: false, method: 'failed' })
      return
    }

    const typed = toPdfBlob(blob)
    let finished = false

    const finish = (result) => {
      if (finished) return
      finished = true
      resolve(result)
    }

    const runDesktop = () => {
      printViaHiddenIframe(typed).then((dialogOk) => {
        if (dialogOk) {
          finish({ ok: true, method: 'dialog' })
          return
        }
        tryWindowPrint(typed, (winOk) => {
          finish(winOk ? { ok: true, method: 'dialog' } : { ok: false, method: 'failed' })
        })
      })
    }

    if (previewIframe?.contentWindow) {
      waitForIframeReady(previewIframe).then((ready) => {
        if (ready && tryPrintIframe(previewIframe)) {
          finish({ ok: true, method: 'dialog' })
          return
        }
        runDesktop()
      })
      return
    }

    runDesktop()
  })
}

function tryWindowPrint (typed, finish) {
  const url = URL.createObjectURL(typed)
  let printWin
  try {
    printWin = window.open(url, '_blank')
  } catch {
    URL.revokeObjectURL(url)
    finish(false)
    return
  }

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

  setTimeout(runPrint, 1200)
  setTimeout(() => {
    if (!printWin.closed) runPrint()
    setTimeout(() => URL.revokeObjectURL(url), blobRevokeDelay())
  }, 2800)
}
