import { useState, useEffect } from 'react'
import Modal from './Modal'
import { formatCurrency } from '../utils/currency'
import toast from 'react-hot-toast'
import { expensesAPI } from '../services'

const INTERPRETATION_ADD = 'add-on-top'
const INTERPRETATION_EXTRACT = 'extract-from-amount'

export default function BulkVatUpdateModal({
  isOpen,
  onClose,
  onSuccess,
  scope = 'all', // 'all' | 'selected' | 'category'
  expenseIds = [],
  categoryId = null,
  categoryName = '',
  noVatCount = 0,
  previewExpenses = []
}) {
  const [interpretation, setInterpretation] = useState(INTERPRETATION_ADD)
  const [vatRate, setVatRate] = useState(0.05)
  const [isTaxClaimable, setIsTaxClaimable] = useState(true)
  const [taxType, setTaxType] = useState('Standard')
  const [applying, setApplying] = useState(false)
  const [preview, setPreview] = useState([])

  const effectiveCount = scope === 'selected' ? expenseIds.length : scope === 'category' ? noVatCount : noVatCount
  const previewList = scope === 'selected' && previewExpenses.length > 0 ? previewExpenses : preview

  useEffect(() => {
    if (!isOpen) return
    if (scope === 'all' && noVatCount > 0) {
      expensesAPI.getExpenses({ noVatOnly: true, pageSize: 5, page: 1 }).then((r) => {
        if (r?.success && r?.data?.items) setPreview(r.data.items)
        else setPreview([])
      }).catch(() => setPreview([]))
    } else if (scope === 'category' && categoryName && noVatCount > 0) {
      expensesAPI.getExpenses({ category: categoryName, noVatOnly: true, pageSize: 5, page: 1 }).then((r) => {
        if (r?.success && r?.data?.items) setPreview(r.data.items)
        else setPreview([])
      }).catch(() => setPreview([]))
    } else if (scope === 'selected') {
      setPreview(previewExpenses.slice(0, 5))
    } else {
      setPreview([])
    }
  }, [isOpen, scope, noVatCount, categoryName, previewExpenses])

  const computeRow = (expense) => {
    const amount = expense.amount ?? 0
    if (interpretation === INTERPRETATION_ADD) {
      const vatAmount = Math.round(amount * vatRate * 100) / 100
      const total = amount + vatAmount
      return { net: amount, vat: vatAmount, total }
    }
    const total = amount
    const net = Math.round(amount / (1 + vatRate) * 100) / 100
    const vatAmount = total - net
    return { net, vat: vatAmount, total }
  }

  const handleApply = async () => {
    setApplying(true)
    try {
      const body = {
        interpretation,
        vatRate: Number(vatRate),
        isTaxClaimable,
        taxType
      }
      if (scope === 'selected' && expenseIds.length > 0) {
        body.expenseIds = expenseIds
      } else if (scope === 'all') {
        body.allNoVat = true
      } else if (scope === 'category' && categoryId) {
        body.categoryId = categoryId
      }
      const res = await expensesAPI.bulkVatUpdate(body)
      if (res?.success) {
        toast.success(res.message || `VAT updated for ${res.data?.updated ?? 0} expenses. Recalculate VAT Return if applicable.`)
        onSuccess?.()
        onClose()
      } else {
        toast.error(res?.message || 'Bulk VAT update failed')
      }
    } catch (e) {
      toast.error(e?.response?.data?.message || e?.message || 'Bulk VAT update failed')
    } finally {
      setApplying(false)
    }
  }

  const displayList = scope === 'selected' ? (previewExpenses.length ? previewExpenses.slice(0, 5) : preview) : preview

  return (
    <Modal isOpen={isOpen} onClose={onClose} title="Bulk VAT Update" size="lg">
      <div className="space-y-4">
        <div>
          <label className="block text-sm font-medium text-gray-700 mb-1">Interpretation</label>
          <div className="flex gap-4">
            <label className="flex items-center gap-2">
              <input
                type="radio"
                checked={interpretation === INTERPRETATION_ADD}
                onChange={() => setInterpretation(INTERPRETATION_ADD)}
              />
              <span>Add VAT on top (amount = net)</span>
            </label>
            <label className="flex items-center gap-2">
              <input
                type="radio"
                checked={interpretation === INTERPRETATION_EXTRACT}
                onChange={() => setInterpretation(INTERPRETATION_EXTRACT)}
              />
              <span>Extract VAT from amount (amount = inclusive)</span>
            </label>
          </div>
        </div>
        <div className="grid grid-cols-2 gap-4">
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">VAT Rate</label>
            <input
              type="number"
              step="0.01"
              min="0"
              max="1"
              value={vatRate}
              onChange={(e) => setVatRate(Number(e.target.value) || 0)}
              className="w-full border border-gray-300 rounded px-3 py-2"
            />
          </div>
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-1">Tax Type</label>
            <select
              value={taxType}
              onChange={(e) => setTaxType(e.target.value)}
              className="w-full border border-gray-300 rounded px-3 py-2"
            >
              <option value="Standard">Standard</option>
              <option value="Exempt">Exempt</option>
              <option value="OutOfScope">Out of Scope</option>
              <option value="Petroleum">Petroleum</option>
            </select>
          </div>
        </div>
        <div>
          <label className="flex items-center gap-2">
            <input
              type="checkbox"
              checked={isTaxClaimable}
              onChange={(e) => setIsTaxClaimable(e.target.checked)}
            />
            <span className="text-sm font-medium text-gray-700">ITC Claimable</span>
          </label>
        </div>
        <div>
          <h4 className="text-sm font-semibold text-gray-800 mb-2">Preview (first 5)</h4>
          <div className="overflow-x-auto border border-lime-300 rounded">
            <table className="min-w-full text-xs">
              <thead className="bg-lime-100">
                <tr>
                  <th className="px-3 py-2 text-left">Category</th>
                  <th className="px-3 py-2 text-right">Old Amount</th>
                  <th className="px-3 py-2 text-right">Net</th>
                  <th className="px-3 py-2 text-right">VAT</th>
                  <th className="px-3 py-2 text-right">Total</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-lime-200">
                {displayList.length === 0 ? (
                  <tr><td colSpan="5" className="px-3 py-4 text-center text-gray-500">No rows to preview</td></tr>
                ) : (
                  displayList.map((exp) => {
                    const row = computeRow(exp)
                    return (
                      <tr key={exp.id} className="hover:bg-lime-50">
                        <td className="px-3 py-2">{exp.categoryName ?? '-'}</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(exp.amount ?? 0)}</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(row.net)}</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(row.vat)}</td>
                        <td className="px-3 py-2 text-right">{formatCurrency(row.total)}</td>
                      </tr>
                    )
                  })
                )}
              </tbody>
            </table>
          </div>
        </div>
        <p className="text-sm text-amber-700 bg-amber-50 px-3 py-2 rounded">
          About {effectiveCount} expense(s) will be updated. Recalculate VAT Return if applicable.
        </p>
        <div className="flex justify-end gap-2">
          <button
            type="button"
            onClick={onClose}
            className="px-4 py-2 border-2 border-lime-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-lime-50"
          >
            Cancel
          </button>
          <button
            type="button"
            onClick={handleApply}
            disabled={applying || effectiveCount === 0}
            className="px-4 py-2 bg-green-600 text-white rounded-md text-sm font-medium hover:bg-green-700 disabled:opacity-50"
          >
            {applying ? 'Applying...' : `Apply VAT to ${effectiveCount} expense(s)`}
          </button>
        </div>
      </div>
    </Modal>
  )
}
