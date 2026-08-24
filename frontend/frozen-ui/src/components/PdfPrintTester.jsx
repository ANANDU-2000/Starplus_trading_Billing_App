import { useState, useCallback, useEffect, useRef } from 'react'
import { Printer, CheckCircle, XCircle, Loader2, X } from 'lucide-react'
import { salesAPI } from '../services'
import { validatePdfBlob } from '../utils/pdfBlob'
import { getHealthUrl, getInvoicePdfUrl } from '../utils/pdfUrls'
import { needsBlobPdfFlow, instantPrintPdfUrl } from '../utils/blobDownload'
import { useAppUpdate } from '../hooks/useAppUpdate'
import { isHonorOrAndroid } from '../utils/pdfHints'

function StatusRow ({ label, ok, detail }) {
  return (
    <div className="flex items-start gap-2 text-xs">
      {ok ? (
        <CheckCircle className="h-4 w-4 text-green-600 shrink-0 mt-0.5" />
      ) : (
        <XCircle className="h-4 w-4 text-red-500 shrink-0 mt-0.5" />
      )}
      <div>
        <span className="font-medium text-gray-800">{label}</span>
        {detail && <p className="text-gray-600">{detail}</p>}
      </div>
    </div>
  )
}

function TestResults ({ results, running, onRun, embeddedBuild, embeddedCommit }) {
  return (
    <div className="space-y-2">
      <p className="text-xs text-gray-600">
        Run before printing on tablet. Build: {embeddedBuild || 'OLD'} ({embeddedCommit || 'legacy'})
      </p>
      {isHonorOrAndroid() && (
        <p className="text-xs text-amber-900 bg-amber-50 rounded p-2">
          Honor/Android: After PDF opens → ⋮ → Share → Print if Print is missing from the menu.
        </p>
      )}
      <button
        type="button"
        onClick={onRun}
        disabled={running}
        className="inline-flex items-center gap-2 px-3 py-1.5 bg-amber-600 text-white rounded text-xs font-semibold hover:bg-amber-700 disabled:opacity-50"
      >
        {running ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Printer className="h-3.5 w-3.5" />}
        Run PDF test
      </button>
      {results && (
        <div className="grid gap-1.5 mt-1 bg-gray-50 rounded border border-gray-100 p-2">
          <StatusRow label="API health" ok={results.api.ok} detail={results.api.detail} />
          <StatusRow label="PDF bytes (%PDF)" ok={results.pdfBytes.ok} detail={results.pdfBytes.detail} />
          <StatusRow label="Direct URL open" ok={results.directUrl.ok} detail={results.directUrl.detail} />
          <StatusRow label="Share API" ok={results.shareApi.ok} detail={results.shareApi.detail} />
          <StatusRow label="Device mode" ok={results.mobile.ok} detail={results.mobile.detail} />
          <StatusRow label="App build" ok={results.build.ok} detail={results.build.detail} />
        </div>
      )}
    </div>
  )
}

/**
 * PDF print diagnostics.
 * - variant="icon" (header): single icon; panel opens only on click
 * - variant="panel": inline panel for Settings (no page-wide banner)
 */
export default function PdfPrintTester ({
  adminOnly = false,
  user,
  variant = 'icon'
}) {
  const { embeddedBuild, embeddedCommit } = useAppUpdate()
  const [open, setOpen] = useState(false)
  const [running, setRunning] = useState(false)
  const [results, setResults] = useState(null)
  const rootRef = useRef(null)

  const isAdmin = user?.role?.toLowerCase() === 'admin'
  const allowed = !adminOnly || isAdmin

  useEffect(() => {
    if (!open || variant !== 'icon') return
    const onDoc = (e) => {
      if (rootRef.current && !rootRef.current.contains(e.target)) setOpen(false)
    }
    const onKey = (e) => {
      if (e.key === 'Escape') setOpen(false)
    }
    document.addEventListener('mousedown', onDoc)
    document.addEventListener('keydown', onKey)
    return () => {
      document.removeEventListener('mousedown', onDoc)
      document.removeEventListener('keydown', onKey)
    }
  }, [open, variant])

  const runTests = useCallback(async () => {
    setRunning(true)
    const out = {
      api: { ok: false, detail: '' },
      pdfBytes: { ok: false, detail: '' },
      directUrl: { ok: false, detail: '' },
      shareApi: { ok: false, detail: '' },
      mobile: { ok: needsBlobPdfFlow(), detail: needsBlobPdfFlow() ? 'Tablet/mobile flow active' : 'Desktop flow' },
      build: { ok: !!embeddedBuild, detail: `${embeddedBuild || 'OLD'} (${embeddedCommit || 'no commit'})` }
    }

    try {
      const healthRes = await fetch(getHealthUrl(), { method: 'GET' })
      out.api.ok = healthRes.ok
      out.api.detail = healthRes.ok ? 'Backend reachable' : `HTTP ${healthRes.status}`
    } catch (err) {
      out.api.detail = err?.message || 'Network error'
    }

    try {
      const salesRes = await salesAPI.getSales({ page: 1, pageSize: 1 })
      const first = salesRes?.data?.items?.[0]
      const saleId = first?.id
      if (!saleId) {
        out.pdfBytes.detail = 'No invoices in database to test'
      } else {
        const blob = await salesAPI.getInvoicePdf(saleId)
        const check = await validatePdfBlob(blob instanceof Blob ? blob : new Blob([blob]))
        out.pdfBytes.ok = check.ok
        out.pdfBytes.detail = check.ok
          ? `Invoice #${first.invoiceNo || saleId} — valid PDF (${check.blob?.size || 0} bytes)`
          : check.message || 'Invalid PDF'

        if (check.ok) {
          const url = getInvoicePdfUrl(saleId, { print: true })
          const printResult = await instantPrintPdfUrl(url)
          out.directUrl.ok = printResult.ok
          out.directUrl.detail = printResult.method === 'dialog'
            ? 'Print dialog triggered for server PDF'
            : printResult.method === 'tab'
              ? 'PDF opened in tab — use ⋮ → Print if dialog did not appear'
              : 'Could not print — allow popups for this site'
        }
      }
    } catch (err) {
      out.pdfBytes.detail = err?.message || 'PDF fetch failed'
    }

    try {
      const dummy = new File([new Uint8Array([0x25, 0x50, 0x44, 0x46])], 'test.pdf', { type: 'application/pdf' })
      out.shareApi.ok = !!navigator.canShare?.({ files: [dummy] })
      out.shareApi.detail = out.shareApi.ok ? 'Share API available (Save/Print)' : 'Share API not available'
    } catch {
      out.shareApi.detail = 'Share API check failed'
    }

    setResults(out)
    setRunning(false)
  }, [embeddedBuild, embeddedCommit])

  if (!allowed) return null

  if (variant === 'panel') {
    return (
      <div className="border border-amber-200 bg-amber-50/50 rounded-lg p-4">
        <h3 className="text-sm font-semibold text-amber-900 mb-2 flex items-center gap-2">
          <Printer className="h-4 w-4" />
          PDF Print Test
        </h3>
        <TestResults
          results={results}
          running={running}
          onRun={runTests}
          embeddedBuild={embeddedBuild}
          embeddedCommit={embeddedCommit}
        />
      </div>
    )
  }

  return (
    <div className="relative" ref={rootRef}>
      <button
        type="button"
        onClick={() => setOpen((v) => !v)}
        className="p-2 hover:bg-blue-700 rounded-lg transition flex items-center justify-center"
        title="PDF Print Test"
        aria-label="PDF Print Test"
        aria-expanded={open}
      >
        <Printer className="h-5 w-5" />
      </button>
      {open && (
        <div className="absolute right-0 top-full mt-2 w-80 max-w-[calc(100vw-1rem)] bg-white text-gray-900 rounded-lg shadow-xl border border-gray-200 z-[60] p-3">
          <div className="flex items-center justify-between mb-2">
            <h3 className="text-sm font-semibold text-gray-900">PDF Print Test</h3>
            <button
              type="button"
              onClick={() => setOpen(false)}
              className="p-1 rounded hover:bg-gray-100"
              aria-label="Close"
            >
              <X className="h-4 w-4 text-gray-500" />
            </button>
          </div>
          <TestResults
            results={results}
            running={running}
            onRun={runTests}
            embeddedBuild={embeddedBuild}
            embeddedCommit={embeddedCommit}
          />
        </div>
      )}
    </div>
  )
}
