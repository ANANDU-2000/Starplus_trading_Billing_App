/**
 * Single source of truth for POS invoice math (mirrors FrozenApi SaleService).
 * VAT is summed per line on the server; here we trust line vatAmount from cart rows.
 */
export function computeInvoiceTotals({ subtotal, vatTotal, discount, roundOff }) {
  const discountValue = typeof discount === 'number' && !Number.isNaN(discount) ? discount : 0
  const roundOffValue = typeof roundOff === 'number' && !Number.isNaN(roundOff) ? roundOff : 0
  const sub = typeof subtotal === 'number' && !Number.isNaN(subtotal) ? subtotal : 0
  const vat = typeof vatTotal === 'number' && !Number.isNaN(vatTotal) ? vatTotal : 0
  const calcBeforeRound = sub + vat - discountValue
  const grandTotal = parseFloat((calcBeforeRound + roundOffValue).toFixed(2))
  return { subtotal: sub, vatTotal: vat, discountValue, roundOffValue, calcBeforeRound, grandTotal }
}

/** Match backend: round-off must be within ±1 of calcBeforeRound to reach whole dirham. */
export function computeAutoRoundOffFromCalc(calcBeforeRound) {
  const rounded = Math.round(calcBeforeRound)
  const diff = rounded - calcBeforeRound
  if (Math.abs(diff) <= 1) return Math.round(diff * 100) / 100
  return 0
}
