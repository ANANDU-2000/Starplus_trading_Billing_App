/**
 * Axios with responseType: 'blob' returns error bodies as Blob — parse JSON message.
 */
export async function parseApiErrorBlobMessage (error, fallback = 'Request failed') {
  try {
    const status = error?.response?.status
    const d = error?.response?.data
    if (d == null) {
      return error?.message || (status ? `Request failed (${status})` : fallback)
    }
    if (typeof d === 'string') {
      try {
        const j = JSON.parse(d)
        return j.message || (Array.isArray(j.errors) ? j.errors.join(', ') : null) || fallback
      } catch {
        return d || fallback
      }
    }
    if (d instanceof Blob) {
      const text = await d.text()
      try {
        const j = JSON.parse(text)
        return j.message || (Array.isArray(j.errors) ? j.errors.join(', ') : null) || fallback
      } catch {
        return text?.slice(0, 300) || fallback
      }
    }
    if (typeof d === 'object' && d.message) return d.message
  } catch {
    /* ignore */
  }
  return error?.message || fallback
}

/**
 * Ensure response is a real PDF before download/open; detect JSON error bodies.
 */
export async function validatePdfBlob (blob) {
  if (!blob || blob.size === 0) {
    return { ok: false, message: 'Empty PDF response' }
  }
  const type = (blob.type || '').toLowerCase()
  if (type.includes('json')) {
    const text = await blob.text()
    try {
      const j = JSON.parse(text)
      return { ok: false, message: j.message || 'Server returned an error instead of PDF' }
    } catch {
      return { ok: false, message: 'Server returned an error instead of PDF' }
    }
  }

  const headBuf = await blob.slice(0, 8).arrayBuffer()
  const head = new Uint8Array(headBuf)
  const magic = String.fromCharCode(...head.slice(0, 4))
  if (magic === '%PDF') {
    return { ok: true, blob }
  }

  const preview = await blob.slice(0, Math.min(500, blob.size)).text()
  const trimmed = preview.trim()
  if (trimmed.startsWith('{')) {
    try {
      const j = JSON.parse(trimmed)
      return { ok: false, message: j.message || (Array.isArray(j.errors) ? j.errors.join(', ') : null) || 'Server returned an error instead of PDF' }
    } catch {
      return { ok: false, message: 'Response is not a valid PDF' }
    }
  }

  if (type.includes('pdf') && blob.size > 100) {
    return { ok: true, blob }
  }

  return { ok: false, message: 'Response is not a valid PDF' }
}

/**
 * Payment receipt endpoint returns HTML bytes (despite /pdf path). Detect JSON error bodies.
 */
export async function validateHtmlReceiptBlob (blob) {
  if (!blob || blob.size === 0) {
    return { ok: false, message: 'Empty receipt' }
  }
  const type = (blob.type || '').toLowerCase()
  if (type.includes('json')) {
    const text = await blob.text()
    try {
      const j = JSON.parse(text)
      return { ok: false, message: j.message || 'Server returned an error instead of a receipt' }
    } catch {
      return { ok: false, message: 'Invalid receipt response' }
    }
  }
  const html = await blob.text()
  const t = html.trimStart()
  if (t.startsWith('{')) {
    try {
      const j = JSON.parse(t)
      return { ok: false, message: j.message || 'Server returned an error instead of a receipt' }
    } catch {
      return { ok: false, message: 'Invalid receipt data' }
    }
  }
  if (
    t.startsWith('<!DOCTYPE') ||
    t.startsWith('<html') ||
    t.startsWith('<HTML') ||
    t.includes('<body') ||
    t.includes('<BODY')
  ) {
    return { ok: true, html }
  }
  return { ok: false, message: 'Receipt is not valid HTML' }
}
