import { useCallback, useEffect, useRef, useState } from 'react'
import { X, Download, Printer, Loader2 } from 'lucide-react'
import toast from 'react-hot-toast'
import { usePdfDocumentStore } from '../stores/pdfDocumentStore'
import { validatePdfBlob, parseApiErrorBlobMessage } from '../utils/pdfBlob'
import {
  savePdfToDevice,
  printPdfBlob,
  isTouchOrTabletDevice,
  triggerBlobDownload
} from '../utils/blobDownload'

export default function PdfDocumentModal () {
  const {
    isOpen,
    title,
    filename,
    fetchPdf,
    mode,
    autoPrint,
    autoSave,
    closePdfDocument
  } = usePdfDocumentStore()

  const [loading, setLoading] = useState(false)
  const [error, setError] = useState(null)
  const [blob, setBlob] = useState(null)
  const [previewUrl, setPreviewUrl] = useState(null)
  const [saving, setSaving] = useState(false)
  const [printing, setPrinting] = useState(false)
  const autoHandledRef = useRef(false)
  const previewUrlRef = useRef(null)

  const revokePreview = useCallback(() => {
    if (previewUrlRef.current) {
      URL.revokeObjectURL(previewUrlRef.current)
      previewUrlRef.current = null
    }
    setPreviewUrl(null)
    setBlob(null)
    setError(null)
    autoHandledRef.current = false
  }, [])

  const handleClose = useCallback(() => {
    revokePreview()
    closePdfDocument()
  }, [revokePreview, closePdfDocument])

  const loadPdf = useCallback(async () => {
    if (!fetchPdf) return
    setLoading(true)
    setError(null)
    revokePreview()
    try {
      const raw = await fetchPdf()
      const data = raw instanceof Blob ? raw : new Blob([raw])
      const check = await validatePdfBlob(data)
      if (!check.ok) {
        throw new Error(check.message || 'Invalid PDF')
      }
      const typed = new Blob([check.blob], { type: 'application/pdf' })
      const url = URL.createObjectURL(typed)
      previewUrlRef.current = url
      setBlob(typed)
      setPreviewUrl(url)
    } catch (err) {
      console.error('[PdfDocumentModal]', err)
      const msg = await parseApiErrorBlobMessage(err, 'Failed to load PDF')
      setError(msg)
      toast.error(msg)
    } finally {
      setLoading(false)
    }
  }, [fetchPdf, revokePreview])

  useEffect(() => {
    if (!isOpen || !fetchPdf) return
    loadPdf()
    return () => revokePreview()
  }, [isOpen, fetchPdf, loadPdf, revokePreview])

  const handleSave = useCallback(async () => {
    if (!blob) return
    setSaving(true)
    try {
      const result = await savePdfToDevice(blob, filename)
      if (result === 'cancelled') return
      if (result === 'picker' || result === 'share') {
        toast.success('PDF saved — check Files or Downloads')
        return
      }
      if (result === 'download') {
        toast.success('Download started — check your downloads folder')
        return
      }
      toast('Use ⋮ menu → Download, or Share → Save to Files', { duration: 5000, icon: 'i' })
    } catch (err) {
      toast.error(err?.message || 'Could not save PDF')
    } finally {
      setSaving(false)
    }
  }, [blob, filename])

  const handlePrint = useCallback(async () => {
    if (!blob) return
    setPrinting(true)
    try {
      const ok = await printPdfBlob(blob)
      if (!ok) {
        toast.error('Print dialog could not open. Try Save PDF then print from Files.')
      }
    } catch (err) {
      toast.error(err?.message || 'Print failed')
    } finally {
      setPrinting(false)
    }
  }, [blob])

  useEffect(() => {
    if (!isOpen || loading || error || !blob || autoHandledRef.current) return
    if (!autoPrint && !autoSave) return
    autoHandledRef.current = true
    const run = async () => {
      if (autoPrint) await handlePrint()
      else if (autoSave) await handleSave()
    }
    void run()
  }, [isOpen, loading, error, blob, autoPrint, autoSave, handlePrint, handleSave])

  if (!isOpen) return null

  const touchHint = isTouchOrTabletDevice()

  return (
    <div className="fixed inset-0 bg-black/60 flex items-center justify-center z-[200] p-2 sm:p-4">
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
          {!loading && !error && previewUrl && (
            <iframe
              title={title}
              src={previewUrl}
              className="w-full h-full min-h-[50vh] border-0 rounded bg-white"
            />
          )}
        </div>

        {touchHint && !loading && !error && blob && (
          <p className="px-4 py-2 text-xs text-gray-600 bg-amber-50 border-t border-amber-100">
            On tablet: tap <strong>Save to device</strong> → choose Files or Downloads.
            For print, tap <strong>Print PDF</strong> and select your printer.
          </p>
        )}

        <div className="flex flex-wrap gap-2 p-3 sm:p-4 border-t bg-gray-50 shrink-0">
          <button
            type="button"
            onClick={handleSave}
            disabled={!blob || saving || loading}
            className="flex items-center gap-2 px-4 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 disabled:opacity-50 text-sm font-medium"
          >
            {saving ? <Loader2 className="h-4 w-4 animate-spin" /> : <Download className="h-4 w-4" />}
            Save to device
          </button>
          <button
            type="button"
            onClick={handlePrint}
            disabled={!blob || printing || loading}
            className="flex items-center gap-2 px-4 py-2.5 bg-gray-700 text-white rounded-lg hover:bg-gray-800 disabled:opacity-50 text-sm font-medium"
          >
            {printing ? <Loader2 className="h-4 w-4 animate-spin" /> : <Printer className="h-4 w-4" />}
            Print PDF
          </button>
          {!touchHint && blob && (
            <button
              type="button"
              onClick={() => {
                triggerBlobDownload(blob, filename)
                toast.success('Download started')
              }}
              disabled={loading}
              className="flex items-center gap-2 px-4 py-2.5 border border-gray-300 rounded-lg hover:bg-gray-100 text-sm"
            >
              <Download className="h-4 w-4" />
              Quick download
            </button>
          )}
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
