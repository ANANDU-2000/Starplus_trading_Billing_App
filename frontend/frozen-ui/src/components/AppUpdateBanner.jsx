import { RefreshCw } from 'lucide-react'
import { useAppUpdate } from '../hooks/useAppUpdate'

export default function AppUpdateBanner () {
  const { updateAvailable, embeddedBuild, remoteBuild, applyUpdate, checking } = useAppUpdate()

  if (!updateAvailable) return null

  return (
    <div className="fixed top-0 left-0 right-0 z-[300] bg-amber-500 text-amber-950 px-3 py-2 shadow-md">
      <div className="max-w-4xl mx-auto flex flex-wrap items-center justify-between gap-2 text-sm">
        <p className="font-medium">
          New app version available ({remoteBuild || 'update'}). You must refresh to use PDF print/download.
          {embeddedBuild && (
            <span className="block text-xs font-normal opacity-90">
              Current: {embeddedBuild}
            </span>
          )}
        </p>
        <button
          type="button"
          onClick={applyUpdate}
          disabled={checking}
          className="flex items-center gap-1.5 px-4 py-1.5 bg-amber-950 text-amber-50 rounded-md font-semibold hover:bg-black shrink-0"
        >
          <RefreshCw className={`h-4 w-4 ${checking ? 'animate-spin' : ''}`} />
          Update now
        </button>
      </div>
      <p className="max-w-4xl mx-auto text-xs mt-1 opacity-90">
        On tablet PWA: remove app from home screen, clear site data in browser, reopen site, reinstall.
      </p>
    </div>
  )
}
