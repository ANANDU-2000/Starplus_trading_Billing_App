import React, { useState, useEffect } from 'react'
import { X, Download, Printer } from 'lucide-react'
import { paymentsAPI } from '../services'
import toast from 'react-hot-toast'
import { validateHtmlReceiptBlob } from '../utils/pdfBlob'
import { triggerBlobDownload } from '../utils/blobDownload'

function downloadHtmlAsFile (html, filename) {
  triggerBlobDownload(new Blob([html], { type: 'text/html;charset=utf-8' }), filename)
}

/**
 * Print HTML without a second window.open (avoids pop-up blockers). Uses a disposable iframe.
 */
function printHtmlInHiddenIframe (html) {
  const iframe = document.createElement('iframe')
  iframe.setAttribute('aria-hidden', 'true')
  iframe.style.cssText =
    'position:fixed;right:0;bottom:0;width:0;height:0;border:0;opacity:0;pointer-events:none'
  document.body.appendChild(iframe)

  const win = iframe.contentWindow
  const doc = iframe.contentDocument || win.document
  doc.open()
  doc.write(html)
  doc.close()

  const cleanup = () => {
    try {
      if (iframe.parentNode) iframe.parentNode.removeChild(iframe)
    } catch {
      /* ignore */
    }
  }

  let fallbackTimer = null
  const onAfterPrint = () => {
    if (fallbackTimer != null) clearTimeout(fallbackTimer)
    win.removeEventListener('afterprint', onAfterPrint)
    cleanup()
  }

  fallbackTimer = setTimeout(() => {
    win.removeEventListener('afterprint', onAfterPrint)
    cleanup()
  }, 10 * 60 * 1000)

  win.addEventListener('afterprint', onAfterPrint)

  const runPrint = () => {
    try {
      win.focus()
      win.print()
    } catch (e) {
      console.error('Print failed:', e)
      toast.error('Print failed. Try Download and open the file instead.')
      if (fallbackTimer != null) clearTimeout(fallbackTimer)
      win.removeEventListener('afterprint', onAfterPrint)
      cleanup()
    }
  }

  requestAnimationFrame(() => {
    setTimeout(runPrint, 100)
  })
}

export default function ReceiptPreviewModal ({ paymentIds = [], isOpen, onClose }) {
  const [loading, setLoading] = useState(false)
  const [receipt, setReceipt] = useState(null)
  const [receiptHtml, setReceiptHtml] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    if (!isOpen || !paymentIds?.length) {
      setReceipt(null)
      setReceiptHtml(null)
      setError(null)
      return
    }
    let cancelled = false
    setLoading(true)
    setError(null)
    const run = async () => {
      try {
        const ids = paymentIds.length === 1 ? paymentIds : [...new Set(paymentIds)]
        const res = ids.length === 1
          ? await paymentsAPI.generateReceipt(ids[0])
          : await paymentsAPI.generateReceiptBatch(ids)
        if (cancelled) return
        const data = res?.data ?? res
        const receiptId = data?.id ?? data?.Id
        if (receiptId == null) throw new Error('Invalid receipt response')
        setReceipt(data)
        const pdfRes = await paymentsAPI.getReceiptPdf(receiptId)
        if (cancelled) return
        const blob = pdfRes instanceof Blob ? pdfRes : new Blob([pdfRes])
        const check = await validateHtmlReceiptBlob(blob)
        if (!check.ok) {
          throw new Error(check.message)
        }
        setReceiptHtml(check.html)
        toast.success(`Receipt ${data.receiptNumber || receiptId} ready`)
      } catch (err) {
        if (!cancelled) {
          const status = err?.response?.status
          const serverMsg = typeof err?.response?.data?.message === 'string' ? err.response.data.message : null
          const userMessage =
            status === 404 || status === 500
              ? 'Receipt could not be loaded. Please try again or contact support.'
              : serverMsg || err?.message || 'Failed to generate receipt'
          setError(userMessage)
          toast.error(userMessage)
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    run()
    return () => { cancelled = true }
  }, [isOpen, paymentIds?.join(',')])

  const handleDownload = () => {
    if (!receiptHtml) {
      toast.error('Nothing to download yet')
      return
    }
    const safeName = String(receipt?.receiptNumber || receipt?.id || 'receipt').replace(/[/\\?%*:|"<>]/g, '-')
    try {
      downloadHtmlAsFile(receiptHtml, `Receipt-${safeName}.html`)
      toast.success('Download started')
    } catch (e) {
      console.error(e)
      toast.error('Download failed')
    }
  }

  const handlePrint = () => {
    if (!receiptHtml) {
      toast.error('Nothing to print yet')
      return
    }
    try {
      printHtmlInHiddenIframe(receiptHtml)
    } catch (e) {
      console.error(e)
      toast.error('Print failed')
    }
  }

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-lg font-bold">
            Payment Receipt Preview
            {receipt?.receiptNumber && (
              <span className="ml-2 text-gray-600 font-normal">
                {receipt.receiptNumber}
                {paymentIds?.length > 1 && (
                  <span className="ml-2 text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded">Combined receipt for {paymentIds.length} payments</span>
                )}
                {receipt.isReprint && (
                  <span className="ml-2 text-xs bg-amber-100 text-amber-800 px-2 py-0.5 rounded">Reprint</span>
                )}
              </span>
            )}
          </h2>
          <button
            type="button"
            onClick={onClose}
            className="p-2 rounded hover:bg-gray-100"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>
        <div className="flex-1 overflow-hidden min-h-0 p-4">
          {loading && (
            <div className="flex items-center justify-center h-64">
              <div className="animate-spin rounded-full h-10 w-10 border-2 border-blue-500 border-t-transparent" />
            </div>
          )}
          {error && (
            <div className="text-red-600 p-4">{error}</div>
          )}
          {!loading && !error && receiptHtml && (
            <iframe
              id="receipt-preview-iframe"
              title="Receipt"
              srcDoc={receiptHtml}
              className="w-full h-[60vh] border rounded bg-white"
            />
          )}
        </div>
        <div className="flex gap-2 p-4 border-t bg-gray-50">
          <button
            type="button"
            onClick={handleDownload}
            disabled={!receiptHtml}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Download className="h-4 w-4" />
            Download
          </button>
          <button
            type="button"
            onClick={handlePrint}
            disabled={!receiptHtml}
            className="flex items-center gap-2 px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Printer className="h-4 w-4" />
            Print
          </button>
          <button
            type="button"
            onClick={onClose}
            className="ml-auto px-4 py-2 border border-gray-300 rounded hover:bg-gray-100"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
