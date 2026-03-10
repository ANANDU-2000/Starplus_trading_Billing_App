import React, { useState, useEffect } from 'react'
import { X, Download, Printer } from 'lucide-react'
import { paymentsAPI } from '../services'
import toast from 'react-hot-toast'

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
        const res = paymentIds.length === 1
          ? await paymentsAPI.generateReceipt(paymentIds[0])
          : await paymentsAPI.generateReceiptBatch(paymentIds)
        if (cancelled) return
        const data = res?.data ?? res
        if (!data?.id) throw new Error('Invalid receipt response')
        setReceipt(data)
        const pdfRes = await paymentsAPI.getReceiptPdf(data.id)
        if (cancelled) return
        const blob = pdfRes instanceof Blob ? pdfRes : new Blob([pdfRes])
        const html = await blob.text()
        setReceiptHtml(html)
        toast.success(`Receipt ${data.receiptNumber || data.id} generated`)
      } catch (err) {
        if (!cancelled) {
          const msg = err?.response?.data?.message || err?.message || 'Failed to generate receipt'
          setError(msg)
          toast.error(msg)
        }
      } finally {
        if (!cancelled) setLoading(false)
      }
    }
    run()
    return () => { cancelled = true }
  }, [isOpen, paymentIds?.join(',')])

  const handleDownload = async () => {
    if (!receipt?.id) return
    try {
      const blob = await paymentsAPI.getReceiptPdf(receipt.id)
      const b = blob instanceof Blob ? blob : new Blob([blob])
      const url = URL.createObjectURL(b)
      const a = document.createElement('a')
      a.href = url
      a.download = `Receipt-${receipt.receiptNumber || receipt.id}.html`
      document.body.appendChild(a)
      a.click()
      document.body.removeChild(a)
      URL.revokeObjectURL(url)
    } catch (e) {
      toast.error('Failed to download receipt')
    }
  }

  const handlePrint = () => {
    const iframe = document.getElementById('receipt-preview-iframe')
    if (iframe?.contentWindow) {
      iframe.contentWindow.print()
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
              className="w-full h-[60vh] border rounded"
              sandbox="allow-same-origin"
            />
          )}
        </div>
        <div className="flex gap-2 p-4 border-t bg-gray-50">
          <button
            type="button"
            onClick={handleDownload}
            disabled={!receipt}
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
