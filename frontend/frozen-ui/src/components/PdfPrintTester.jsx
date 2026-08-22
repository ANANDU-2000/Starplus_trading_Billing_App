import { useState, useCallback } from 'react'
import { ChevronDown, ChevronUp, Printer, CheckCircle, XCircle, Loader2 } from 'lucide-react'
import { salesAPI } from '../services'
import { validatePdfBlob } from '../utils/pdfBlob'
import { getHealthUrl, getInvoicePdfUrl } from '../utils/pdfUrls'
import { openPdfDirectUrl, needsBlobPdfFlow, instantPrintPdfUrl } from '../utils/blobDownload'
import { useAppUpdate } from '../hooks/useAppUpdate'
import { isHonorOrAndroid } from '../utils/pdfHints'

const STORAGE_KEY = 'pdf_tester_collapsed'

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

export default function PdfPrintTester ({ adminOnly = false, user }) {
  const { embeddedBuild, embeddedCommit } = useAppUpdate()
  const [collapsed, setCollapsed] = useState(() => {
    try {
      return localStorage.getItem(STORAGE_KEY) === '1'
    } catch {
      return false
    }
  })
  const [running, setRunning] = useState(false)
  const [results, setResults] = useState(null)

  if (adminOnly && user?.role?.toLowerCase() !== 'admin') return null

  const toggleCollapsed = () => {
    const next = !collapsed
    setCollapsed(next)
    try {
      localStorage.setItem(STORAGE_KEY, next ? '1' : '0')
    } catch {
      /* ignore */
    }
  }

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

  return (
    <div className="bg-amber-50 border-b border-amber-200 shrink-0">
      <button
        type="button"
        onClick={toggleCollapsed}
        className="w-full flex items-center justify-between px-3 py-2 text-left text-sm font-medium text-amber-900"
      >
        <span className="flex items-center gap-2">
          <Printer className="h-4 w-4" />
          PDF Print Tester
          {results && (
            <span className="text-xs font-normal text-amber-700">
              {Object.values(results).filter((r) => r.ok).length}/{Object.keys(results).length} passed
            </span>
          )}
        </span>
        {collapsed ? <ChevronDown className="h-4 w-4" /> : <ChevronUp className="h-4 w-4" />}
      </button>

      {!collapsed && (
        <div className="px-3 pb-3 space-y-2">
          <p className="text-xs text-amber-800">
            Run this on the tablet before printing invoices. Build: {embeddedBuild || 'OLD'} ({embeddedCommit || 'legacy'})
          </p>
          {isHonorOrAndroid() && (
            <p className="text-xs text-amber-900 bg-amber-100 rounded p-2">
              Honor/Android: After PDF opens → ⋮ → Share → Print (Print may not appear in Chrome menu directly).
            </p>
          )}
          <button
            type="button"
            onClick={runTests}
            disabled={running}
            className="inline-flex items-center gap-2 px-3 py-1.5 bg-amber-600 text-white rounded text-xs font-semibold hover:bg-amber-700 disabled:opacity-50"
          >
            {running ? <Loader2 className="h-3.5 w-3.5 animate-spin" /> : <Printer className="h-3.5 w-3.5" />}
            Run PDF test
          </button>
          {results && (
            <div className="grid gap-1.5 mt-2 bg-white rounded border border-amber-100 p-2">
              <StatusRow label="API health" ok={results.api.ok} detail={results.api.detail} />
              <StatusRow label="PDF bytes (%PDF)" ok={results.pdfBytes.ok} detail={results.pdfBytes.detail} />
              <StatusRow label="Direct URL open" ok={results.directUrl.ok} detail={results.directUrl.detail} />
              <StatusRow label="Share API" ok={results.shareApi.ok} detail={results.shareApi.detail} />
              <StatusRow label="Device mode" ok={results.mobile.ok} detail={results.mobile.detail} />
              <StatusRow label="App build" ok={results.build.ok} detail={results.build.detail} />
            </div>
          )}
        </div>
      )}
    </div>
  )
}
