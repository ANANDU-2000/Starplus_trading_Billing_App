import { useCallback, useEffect, useRef, useState } from 'react'
import { X, Download, Printer, Loader2, ExternalLink } from 'lucide-react'
import toast from 'react-hot-toast'
import { usePdfDocumentStore } from '../stores/pdfDocumentStore'
import { validatePdfBlob, parseApiErrorBlobMessage } from '../utils/pdfBlob'
import {
  savePdfToDevice,
  printPdfBlobWhenReady,
  needsBlobPdfFlow,
  openPdfDirectUrl
} from '../utils/blobDownload'
import { mobilePrintPdf, toastPrintResult } from '../utils/pdfMobile'
import { getPrintHintText, isHonorOrAndroid } from '../utils/pdfHints'

export default function PdfDocumentModal () {
  const {
    isOpen,
    title,
    filename,
    fetchPdf,
    directUrl,
    mode,
    closePdfDocument
  } = usePdfDocumentStore()

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [blob, setBlob] = useState(null)
  const [previewUrl, setPreviewUrl] = useState(null)
  const [iframeReady, setIframeReady] = useState(false)
  const [saving, setSaving] = useState(false)
  const [printing, setPrinting] = useState(false)
  const previewIframeRef = useRef(null)
  const previewUrlRef = useRef(null)
  const autoActionDoneRef = useRef(false)

  const isMobile = needsBlobPdfFlow()

  const revokePreview = useCallback(() => {
    if (previewUrlRef.current) {
      URL.revokeObjectURL(previewUrlRef.current)
      previewUrlRef.current = null
    }
    setPreviewUrl(null)
    setBlob(null)
    setError(null)
    setIframeReady(false)
    autoActionDoneRef.current = false
  }, [])

  const handleClose = useCallback(() => {
    revokePreview()
    closePdfDocument()
  }, [revokePreview, closePdfDocument])

  const loadPdf = useCallback(async () => {
    if (!fetchPdf) return
    setLoading(true)
    setError(null)
    setIframeReady(false)
    autoActionDoneRef.current = false
    revokePreview()
    try {
      const raw = await fetchPdf()
      const data = raw instanceof Blob ? raw : new Blob([raw])
      const check = await validatePdfBlob(data)
      if (!check.ok) {
        throw new Error(check.message || 'Invalid PDF')
      }
      const typed = new Blob([check.blob], { type: 'application/pdf' })
      if (!isMobile) {
        const url = URL.createObjectURL(typed)
        previewUrlRef.current = url
        setPreviewUrl(url)
      }
      setBlob(typed)
    } catch (err) {
      console.error('[PdfDocumentModal]', err)
      const msg = await parseApiErrorBlobMessage(err, 'Failed to load PDF')
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }, [fetchPdf, revokePreview, isMobile])

  useEffect(() => {
    if (!isOpen) return
    if (fetchPdf) loadPdf()
    return () => revokePreview()
  }, [isOpen, fetchPdf, loadPdf, revokePreview])

  const handleOpenInChrome = useCallback(() => {
    const url = directUrl || previewUrl
    if (url && openPdfDirectUrl(url)) {
      toast('PDF opened in Chrome', { duration: 4000 })
      return
    }
    toast.error('Could not open PDF')
  }, [directUrl, previewUrl])

  const handleSave = useCallback(async () => {
    if (!blob) return
    setSaving(true)
    try {
      const result = await savePdfToDevice(blob, filename)
      if (result === 'cancelled') return
      if (result === 'picker' || result === 'share') {
        toast.success('PDF saved — check your Downloads or Files folder')
        return
      }
      if (result === 'download') {
        toast.success('PDF saved to downloads folder')
        return
      }
      handleOpenInChrome()
      toast('Share → Save to Files', { duration: 6000, icon: 'ℹ️' })
    } catch (err) {
      toast.error(err?.message || 'Could not save PDF')
    } finally {
      setSaving(false)
    }
  }, [blob, filename, handleOpenInChrome])

  const handlePrint = useCallback(async () => {
    if (!blob && !directUrl) return
    setPrinting(true)
    try {
      if (isMobile) {
        const result = await mobilePrintPdf({ blob, filename, directUrl })
        toastPrintResult(result, toast)
        return
      }
      const result = await printPdfBlobWhenReady(blob, previewIframeRef.current)
      if (result.ok) {
        toastPrintResult(result, toast)
      } else {
        toast.error('Could not open print. Save the PDF first, then print from your file manager.')
      }
    } catch (err) {
      toast.error(err?.message || 'Print failed')
    } finally {
      setPrinting(false)
    }
  }, [blob, filename, directUrl, isMobile])

  // Auto print or save on mobile/desktop when opened via print/download mode
  useEffect(() => {
    if (!blob || loading || error || autoActionDoneRef.current) return
    if (mode !== 'print' && mode !== 'download') return
    if (!isMobile && !iframeReady) return
    autoActionDoneRef.current = true
    if (mode === 'print') {
      void handlePrint()
    } else {
      void handleSave()
    }
  }, [blob, iframeReady, loading, error, mode, handlePrint, handleSave, isMobile])

  if (!isOpen) return null

  const emphasizePrint = mode === 'print'
  const emphasizeSave = mode === 'download'
  const actionsReady = blob && !loading && (isMobile || iframeReady)
  const hintText = isHonorOrAndroid()
    ? 'Tap Open PDF → ⋮ → Share → Print (Honor tablets may not show Print in the menu)'
    : getPrintHintText()

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[200] p-2 sm:p-4 print:hidden">
      <div className="bg-white rounded-lg shadow-xl w-full max-w-5xl max-h-[95vh] flex flex-col">
        <div className="flex items-center justify-between p-3 sm:p-4 border-b shrink-0">
          <h2 className="text-base sm:text-lg font-bold text-gray-900 truncate pr-2">{title}</h2>
          <button
            type="button"
            onClick={handleClose}
            className="p-2 rounded hover:bg-gray-100 shrink-0"
            aria-label="Close"
          >
            <X className="h-5 w-5" />
          </button>
        </div>

        <div className="flex-1 min-h-0 overflow-hidden bg-gray-100 p-2">
          {loading && (
            <div className="flex flex-col items-center justify-center h-full min-h-[40vh] gap-3 text-gray-600">
              <Loader2 className="h-10 w-10 animate-spin text-blue-600" />
              <span>Loading PDF…</span>
            </div>
          )}
          {error && !loading && (
            <div className="p-6 text-red-600 text-center">{error}</div>
          )}
          {!loading && !error && blob && isMobile && (
            <div className="flex flex-col items-center justify-center h-full min-h-[40vh] gap-4 p-6 text-center">
              <p className="text-sm text-gray-700">
                Your <strong>real PDF</strong> is ready from the server.
              </p>
              <button
                type="button"
                onClick={handleOpenInChrome}
                className="flex items-center gap-2 px-6 py-3 bg-blue-600 text-white rounded-lg font-semibold text-base"
              >
                <ExternalLink className="h-5 w-5" />
                Open PDF in Chrome
              </button>
              <p className="text-xs text-gray-500 max-w-sm">{hintText}</p>
            </div>
          )}
          {!loading && !error && previewUrl && !isMobile && (
            <iframe
              ref={previewIframeRef}
              title={title}
              src={previewUrl}
              onLoad={() => setIframeReady(true)}
              className="w-full h-full min-h-[50vh] border-0 rounded bg-white"
            />
          )}
        </div>

        {!loading && !error && blob && (
          <p className="px-4 py-2 text-sm text-gray-800 bg-green-50 border-t border-green-200 font-medium">
            This is your <strong>real PDF</strong> from the server — not the app screen.
          </p>
        )}

        {isMobile && !loading && !error && blob && (
          <p className="px-4 py-2 text-xs text-gray-600 bg-amber-50 border-t border-amber-100">
            {hintText}
          </p>
        )}

        {!isMobile && !loading && !error && blob && (
          <p className="px-4 py-2 text-xs text-gray-600 bg-blue-50 border-t border-blue-100">
            Do not use browser Ctrl+P on the app — use <strong>Print PDF</strong> in this window.
          </p>
        )}

        <div className="flex flex-wrap gap-2 p-3 sm:p-4 border-t bg-gray-50 shrink-0">
          {isMobile && (
            <button
              type="button"
              onClick={handleOpenInChrome}
              disabled={!actionsReady}
              className="flex items-center gap-2 px-4 py-2.5 rounded-lg disabled:opacity-50 text-sm font-medium bg-indigo-600 text-white hover:bg-indigo-700"
            >
              <ExternalLink className="h-4 w-4" />
              Open in Chrome
            </button>
          )}
          <button
            type="button"
            onClick={handleSave}
            disabled={!actionsReady || saving}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-lg disabled:opacity-50 text-sm font-medium ${
              emphasizeSave
                ? 'bg-blue-600 text-white hover:bg-blue-700 ring-2 ring-blue-400'
                : 'bg-blue-600 text-white hover:bg-blue-700'
            }`}
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            Save to device
          </button>
          <button
            type="button"
            onClick={handlePrint}
            disabled={!actionsReady || printing}
            className={`flex items-center gap-2 px-4 py-2.5 rounded-lg disabled:opacity-50 text-sm font-medium ${
              emphasizePrint
                ? 'bg-gray-800 text-white hover:bg-gray-900 ring-2 ring-gray-400'
                : 'bg-gray-700 text-white hover:bg-gray-800'
            }`}
          >
            {printing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Printer className="h-4 w-4" />}
            Print PDF
          </button>
          <button
            type="button"
            onClick={handleClose}
            className="ml-auto px-4 py-2.5 border border-gray-300 rounded-lg hover:bg-gray-100 text-sm"
          >
            Close
          </button>
        </div>
      </div>
    </div>
  )
}
