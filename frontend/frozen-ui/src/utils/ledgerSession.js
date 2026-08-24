const LEDGER_SESSION_KEY = 'starplus_ledger_session'

/** Prefer localStorage so F5 / PWA reloads keep customer + date filters. */
function storage () {
  try {
    return window.localStorage
  } catch {
    return null
  }
}

export function saveLedgerSession (state) {
  const store = storage()
  if (!store) return
  try {
    store.setItem(LEDGER_SESSION_KEY, JSON.stringify(state))
  } catch {
    // ignore quota / private mode
  }
}

export function loadLedgerSession () {
  const store = storage()
  if (!store) return null
  try {
    const raw = store.getItem(LEDGER_SESSION_KEY)
    if (raw) return JSON.parse(raw)
  } catch {
    /* ignore */
  }
  // Migrate older sessionStorage copies once
  try {
    const legacy = sessionStorage.getItem(LEDGER_SESSION_KEY)
    if (legacy) {
      store.setItem(LEDGER_SESSION_KEY, legacy)
      sessionStorage.removeItem(LEDGER_SESSION_KEY)
      return JSON.parse(legacy)
    }
  } catch {
    /* ignore */
  }
  return null
}

export function formatLedgerPeriod (from, to) {
  const fmt = (d) =>
    new Date(`${d}T12:00:00`).toLocaleDateString('en-GB', {
      day: '2-digit',
      month: 'short',
      year: 'numeric'
    })
  return `Period: ${fmt(from)} – ${fmt(to)}`
}

/** Defaults used when nothing is saved yet */
export function defaultLedgerDateRange () {
  return {
    from: new Date(new Date().setDate(1)).toISOString().split('T')[0],
    to: new Date().toISOString().split('T')[0]
  }
}
