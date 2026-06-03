import { useCallback, useEffect, useState } from 'react'

const STORAGE_KEY = 'starplus_app_build'

export function getStoredBuild () {
  try {
    return localStorage.getItem(STORAGE_KEY) || ''
  } catch {
    return ''
  }
}

export function setStoredBuild (build) {
  try {
    if (build) localStorage.setItem(STORAGE_KEY, build)
  } catch {
    /* ignore */
  }
}

export function useAppUpdate () {
  const embeddedBuild = import.meta.env.VITE_APP_BUILD || ''
  const [updateAvailable, setUpdateAvailable] = useState(false)
  const [remoteBuild, setRemoteBuild] = useState(null)
  const [checking, setChecking] = useState(false)

  const checkForUpdate = useCallback(async () => {
    if (!embeddedBuild) return
    setChecking(true)
    try {
      const res = await fetch(`/version.json?t=${Date.now()}`, {
        cache: 'no-store',
        headers: { Accept: 'application/json' }
      })
      if (!res.ok) return
      const data = await res.json()
      const remote = data?.build || ''
      setRemoteBuild(remote)
      const stored = getStoredBuild()
      if (!stored) {
        setStoredBuild(embeddedBuild)
      }
      if (remote && remote !== embeddedBuild) {
        setUpdateAvailable(true)
      }
    } catch {
      /* offline or dev without version.json */
    } finally {
      setChecking(false)
    }
  }, [embeddedBuild])

  useEffect(() => {
    if (embeddedBuild) {
      const stored = getStoredBuild()
      if (!stored) setStoredBuild(embeddedBuild)
    }
    checkForUpdate()
    const interval = setInterval(checkForUpdate, 5 * 60 * 1000)
    return () => clearInterval(interval)
  }, [embeddedBuild, checkForUpdate])

  const applyUpdate = useCallback(() => {
    if (remoteBuild) setStoredBuild(remoteBuild)
    window.location.reload()
  }, [remoteBuild])

  return {
    embeddedBuild,
    remoteBuild,
    updateAvailable,
    checking,
    applyUpdate,
    checkForUpdate
  }
}
