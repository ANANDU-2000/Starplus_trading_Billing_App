/** In-memory cache of validated invoice PDF blobs keyed by saleId */

const cache = new Map()
const MAX_ENTRIES = 20

export function setCachedInvoicePdf (saleId, blob) {
  if (saleId == null || !blob) return
  const id = String(saleId)
  if (cache.size >= MAX_ENTRIES) {
    const first = cache.keys().next().value
    cache.delete(first)
  }
  cache.set(id, blob)
}

export function getCachedInvoicePdf (saleId) {
  if (saleId == null) return null
  return cache.get(String(saleId)) || null
}

export function clearCachedInvoicePdf (saleId) {
  if (saleId == null) {
    cache.clear()
    return
  }
  cache.delete(String(saleId))
}
