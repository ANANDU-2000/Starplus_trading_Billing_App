import { RefreshCw, AlertTriangle } from 'lucide-react'
import { useAppUpdate } from '../hooks/useAppUpdate'

export default function AppUpdateBanner () {
  const {
    updateAvailable,
    embeddedBuild,
    embeddedCommit,
    remoteBuild,
    remoteCommit,
    applyUpdate,
    checking
  } = useAppUpdate()

  if (!updateAvailable) return null

  const isLegacy = !embeddedBuild || !embeddedCommit

  return (
    <div className="fixed inset-0 z-[500] bg-black/70 flex items-center justify-center p-4">
      <div className="bg-white rounded-xl shadow-2xl max-w-md w-full p-6 text-center">
        <AlertTriangle className="h-12 w-12 text-amber-500 mx-auto mb-3" />
        <h2 className="text-lg font-bold text-gray-900 mb-2">
          {isLegacy ? 'Old app version — PDF will not work' : 'New version required'}
        </h2>
        <p className="text-sm text-gray-600 mb-4">
          {isLegacy
            ? 'Your tablet is using a cached old copy (Print Receipt / fake download messages). You must update before invoice PDF print or save will work.'
            : 'A new version was deployed. Refresh to fix POS Print and Save PDF.'}
        </p>
        {embeddedBuild && (
          <p className="text-xs text-gray-500 mb-1">Your app: {embeddedBuild} ({embeddedCommit || 'no commit'})</p>
        )}
        {remoteBuild && (
          <p className="text-xs text-gray-500 mb-4">Latest: {remoteBuild} ({remoteCommit || ''})</p>
        )}
        <button
          type="button"
          onClick={applyUpdate}
          disabled={checking}
          className="w-full flex items-center justify-center gap-2 px-4 py-3 bg-blue-600 text-white rounded-lg font-semibold hover:bg-blue-700"
        >
          <RefreshCw className={`h-5 w-5 ${checking ? 'animate-spin' : ''}`} />
          Update app now
        </button>
        <p className="text-xs text-gray-500 mt-4">
          Honor/PWA: remove from home screen → Chrome → Clear site data → reopen → reinstall.
        </p>
      </div>
    </div>
  )
}
