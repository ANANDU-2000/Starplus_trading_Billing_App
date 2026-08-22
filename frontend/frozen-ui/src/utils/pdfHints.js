import { isLikelyMobileBrowser } from './blobDownload'

const HINT_KEY = 'pdf_hint_seen'

export function isHonorOrAndroid () {
  if (typeof navigator === 'undefined') return false
  return /Android|Huawei|HONOR/i.test(navigator.userAgent)
}

export function getPrintHintText () {
  if (isHonorOrAndroid()) {
    return 'PDF opened in Chrome — tap ⋮ → Share → Print (or Open with → Chrome PDF Viewer)'
  }
  if (isLikelyMobileBrowser()) {
    return 'PDF opened — tap ⋮ → Print in the PDF tab'
  }
  return 'Print dialog opened — print the PDF, not this app screen'
}

export function getDownloadHintText () {
  if (isLikelyMobileBrowser()) {
    return 'Choose Save to Files or Downloads in the Share sheet'
  }
  return 'PDF saved to downloads'
}

export function getPrintResultToast (method) {
  if (method === 'dialog') {
    return { type: 'success', message: 'Print dialog opened' }
  }
  if (method === 'share') {
    return { type: 'info', message: 'Share sheet opened — pick Print or Save to Files' }
  }
  if (method === 'tab') {
    return { type: 'info', message: getPrintHintText(), duration: 8000 }
  }
  if (method === 'cancelled') {
    return { type: 'silent' }
  }
  return { type: 'error', message: 'Could not open PDF for printing — try Save first' }
}

export function markPrintHintSeen () {
  try {
    localStorage.setItem(HINT_KEY, '1')
  } catch {
    /* ignore */
  }
}

export function shouldShowPrintCoachMark () {
  try {
    return !localStorage.getItem(HINT_KEY)
  } catch {
    return false
  }
}
