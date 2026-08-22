const LEDGER_SESSION_KEY = 'starplus_ledger_session'

export function saveLedgerSession (state) {
  try {
    sessionStorage.setItem(LEDGER_SESSION_KEY, JSON.stringify(state))
  } catch {
    // ignore quota / private mode
  }
}

export function loadLedgerSession () {
  try {
    const raw = sessionStorage.getItem(LEDGER_SESSION_KEY)
    return raw ? JSON.parse(raw) : null
  } catch {
    return null
  }
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
