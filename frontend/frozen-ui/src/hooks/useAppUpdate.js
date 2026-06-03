import { useCallback, useEffect, useState } from 'react'

const STORAGE_KEY = 'starplus_app_build'
const STORAGE_COMMIT_KEY = 'starplus_app_commit'

export function getStoredBuild () {
  try {
    return localStorage.getItem(STORAGE_KEY) || ''
  } catch {
    return ''
  }
}

export function setStoredBuild (build, commit) {
  try {
    if (build) localStorage.setItem(STORAGE_KEY, build)
    if (commit) localStorage.setItem(STORAGE_COMMIT_KEY, commit)
  } catch {
    /* ignore */
  }
}

export function useAppUpdate () {
  const embeddedBuild = import.meta.env.VITE_APP_BUILD || ''
  const embeddedCommit = import.meta.env.VITE_APP_COMMIT || ''
  const [updateAvailable, setUpdateAvailable] = useState(false)
  const [remoteBuild, setRemoteBuild] = useState(null)
  const [remoteCommit, setRemoteCommit] = useState(null)
  const [checking, setChecking] = useState(false)

  const checkForUpdate = useCallback(async () => {
    setChecking(true)
    try {
      const res = await fetch(`/version.json?t=${Date.now()}`, {
        cache: 'no-store',
        headers: { Accept: 'application/json' }
      })
      if (!res.ok) return
      const data = await res.json()
      const remote = data?.build || ''
      const commit = data?.commit || ''
      setRemoteBuild(remote)
      setRemoteCommit(commit)

      if (embeddedBuild) {
        const stored = getStoredBuild()
        if (!stored) setStoredBuild(embeddedBuild, embeddedCommit)
      }

      const buildMismatch = remote && embeddedBuild && remote !== embeddedBuild
      const commitMismatch = commit && embeddedCommit && commit !== embeddedCommit
      const legacyApp = !embeddedBuild || !embeddedCommit

      if (legacyApp || buildMismatch || commitMismatch) {
        setUpdateAvailable(true)
      } else {
        setUpdateAvailable(false)
      }
    } catch {
      /* offline or dev without version.json */
    } finally {
      setChecking(false)
    }
  }, [embeddedBuild, embeddedCommit])

  useEffect(() => {
    checkForUpdate()
    const interval = setInterval(checkForUpdate, 2 * 60 * 1000)
    return () => clearInterval(interval)
  }, [checkForUpdate])

  const applyUpdate = useCallback(async () => {
    if (remoteBuild) setStoredBuild(remoteBuild, remoteCommit)
    try {
      if ('caches' in window) {
        const keys = await caches.keys()
        await Promise.all(keys.map((k) => caches.delete(k)))
      }
    } catch {
      /* ignore */
    }
    const url = new URL(window.location.href)
    url.searchParams.set('_refresh', String(Date.now()))
    window.location.replace(url.toString())
  }, [remoteBuild, remoteCommit])

  return {
    embeddedBuild,
    embeddedCommit,
    remoteBuild,
    remoteCommit,
    updateAvailable,
    checking,
    applyUpdate,
    checkForUpdate
  }
}
