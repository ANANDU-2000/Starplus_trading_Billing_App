import React, { useState, useEffect } from 'react'
import { X, Download, Printer } from 'lucide-react'
import { paymentsAPI } from '../services'
import toast from 'react-hot-toast'
import {
  downloadReceiptPdf,
  loadPdfBlobUrl,
  openReceiptPdfForPrint
} from '../utils/invoicePdfActions'
import { formatCurrency } from '../utils/currency'

export default function ReceiptPreviewModal ({ paymentIds = [], isOpen, onClose }) {
  const [loading, setLoading] = useState(false)
  const [receipt, setReceipt] = useState(null)
  const [error, setError] = useState(null)

  useEffect(() => {
    if (!isOpen || !paymentIds?.length) {
      setReceipt(null)
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

  const receiptId = receipt?.id ?? receipt?.Id
  const [previewUrl, setPreviewUrl] = useState(null)
  const [previewLoading, setPreviewLoading] = useState(false)

  useEffect(() => {
    if (!receiptId) {
      setPreviewUrl(null)
      return undefined
    }
    let cancelled = false
    let activeUrl = null
    setPreviewLoading(true)
    const load = async () => {
      try {
        const url = await loadPdfBlobUrl(() => paymentsAPI.getReceiptPdf(receiptId))
        activeUrl = url
        if (!cancelled) setPreviewUrl(url)
      } catch (err) {
        if (!cancelled) {
          console.error('[receipt-preview]', err)
          setPreviewUrl(null)
        }
      } finally {
        if (!cancelled) setPreviewLoading(false)
      }
    }
    load()
    return () => {
      cancelled = true
      if (activeUrl) URL.revokeObjectURL(activeUrl)
      setPreviewUrl(null)
    }
  }, [receiptId])

  const handleDownload = async () => {
    if (!receiptId) {
      toast.error('Nothing to download yet')
      return
    }
    await downloadReceiptPdf(receiptId, receipt?.receiptNumber || receipt?.ReceiptNumber)
  }

  const handlePrint = async () => {
    if (!receiptId) {
      toast.error('Nothing to print yet')
      return
    }
    await openReceiptPdfForPrint(receiptId)
  }

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 bg-black/50 flex items-center justify-center z-[100] p-4">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-4xl max-h-[90vh] flex flex-col">
        <div className="flex items-center justify-between p-4 border-b">
          <h2 className="text-lg font-bold">
            Payment Receipt
            {receipt?.receiptNumber && (
              <span className="ml-2 text-gray-600 font-normal">
                {receipt.receiptNumber}
                {paymentIds?.length > 1 && (
                  <span className="ml-2 text-xs bg-blue-100 text-blue-800 px-2 py-0.5 rounded">
                    Combined receipt for {paymentIds.length} payments
                  </span>
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
          {!loading && !error && receipt && (
            <div className="space-y-4 h-full flex flex-col">
              <div className="grid grid-cols-1 sm:grid-cols-3 gap-3 text-sm">
                <div className="bg-gray-50 rounded p-3">
                  <div className="text-gray-500">Customer</div>
                  <div className="font-medium">{receipt.customerName || receipt.CustomerName || 'N/A'}</div>
                </div>
                <div className="bg-gray-50 rounded p-3">
                  <div className="text-gray-500">Total Paid</div>
                  <div className="font-medium">{formatCurrency(receipt.totalAmount ?? receipt.TotalAmount ?? 0)}</div>
                </div>
                <div className="bg-gray-50 rounded p-3">
                  <div className="text-gray-500">Date</div>
                  <div className="font-medium">
                    {receipt.generatedAt
                      ? new Date(receipt.generatedAt).toLocaleDateString('en-GB')
                      : new Date().toLocaleDateString('en-GB')}
                  </div>
                </div>
              </div>
              {previewLoading && (
                <div className="flex items-center justify-center min-h-[40vh] text-sm text-gray-500">
                  Loading PDF preview…
                </div>
              )}
              {!previewLoading && previewUrl && (
                <iframe
                  title="Receipt PDF Preview"
                  src={previewUrl}
                  className="w-full flex-1 min-h-[50vh] border rounded bg-white"
                />
              )}
              {!previewLoading && !previewUrl && receiptId && (
                <p className="text-sm text-gray-500 p-4">
                  Preview unavailable. Use Print PDF or Download PDF below.
                </p>
              )}
            </div>
          )}
        </div>
        <div className="flex gap-2 p-4 border-t bg-gray-50">
          <button
            type="button"
            onClick={handleDownload}
            disabled={!receiptId}
            className="flex items-center gap-2 px-4 py-2 bg-blue-600 text-white rounded hover:bg-blue-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Download className="h-4 w-4" />
            Download PDF
          </button>
          <button
            type="button"
            onClick={handlePrint}
            disabled={!receiptId}
            className="flex items-center gap-2 px-4 py-2 bg-gray-600 text-white rounded hover:bg-gray-700 disabled:opacity-50 disabled:cursor-not-allowed"
          >
            <Printer className="h-4 w-4" />
            Print PDF
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
