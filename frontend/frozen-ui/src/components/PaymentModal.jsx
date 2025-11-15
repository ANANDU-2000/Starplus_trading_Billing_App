import { useState, useEffect } from 'react'
import { X, Wallet, DollarSign, Calendar, FileText } from 'lucide-react'
import toast from 'react-hot-toast'
import { paymentsAPI, salesAPI } from '../services'

const PaymentModal = ({ isOpen, onClose, invoiceId, customerId, onPaymentSuccess }) => {
  const [loading, setLoading] = useState(false)
  const [invoice, setInvoice] = useState(null)
  const [formData, setFormData] = useState({
    amount: 0,
    mode: 'CASH',
    reference: '',
    paymentDate: new Date().toISOString().split('T')[0]
  })

  useEffect(() => {
    if (isOpen && invoiceId) {
      loadInvoiceAmount()
    }
  }, [isOpen, invoiceId])

  const loadInvoiceAmount = async () => {
    try {
      const response = await salesAPI.getInvoiceAmount?.(invoiceId) || await paymentsAPI.getInvoiceAmount?.(invoiceId)
      if (response?.data?.success && response.data.data) {
        const invoiceData = response.data.data
        setInvoice(invoiceData)
        // Auto-fill outstanding amount
        setFormData(prev => ({
          ...prev,
          amount: invoiceData.outstandingAmount || 0
        }))
      } else {
        // Fallback: try to get sale details
        const saleResponse = await salesAPI.getSale(invoiceId)
        if (saleResponse?.data?.success && saleResponse.data.data) {
          const sale = saleResponse.data.data
          const outstanding = (sale.grandTotal || sale.totalAmount || 0) - (sale.paidAmount || 0)
          setInvoice({
            invoiceNo: sale.invoiceNo,
            totalAmount: sale.grandTotal || sale.totalAmount || 0,
            paidAmount: sale.paidAmount || 0,
            outstandingAmount: outstanding
          })
          setFormData(prev => ({
            ...prev,
            amount: outstanding
          }))
        }
      }
    } catch (error) {
      console.error('Failed to load invoice amount:', error)
      toast.error('Failed to load invoice details')
    }
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    
    if (loading) {
      toast.error('Please wait, operation in progress...')
      return
    }

    if (!formData.amount || formData.amount <= 0) {
      toast.error('Payment amount must be greater than zero')
      return
    }

    if (!customerId && !invoiceId) {
      toast.error('Customer or Invoice is required')
      return
    }

    if (invoice && formData.amount > invoice.outstandingAmount) {
      toast.error(`Amount exceeds outstanding by ${(formData.amount - invoice.outstandingAmount).toFixed(2)}. Outstanding: ${invoice.outstandingAmount.toFixed(2)}`)
      return
    }

    setLoading(true)
    try {
      // Generate idempotency key for duplicate prevention
      const idempotencyKey = crypto.randomUUID()
      
      const paymentData = {
        saleId: invoiceId || null,
        customerId: customerId || null,
        amount: parseFloat(formData.amount),
        mode: formData.mode.toUpperCase(), // Ensure uppercase: CASH, CHEQUE, ONLINE, CREDIT
        reference: formData.reference || null,
        paymentDate: formData.paymentDate ? new Date(formData.paymentDate).toISOString() : new Date().toISOString()
      }

      const response = await paymentsAPI.createPayment(paymentData, idempotencyKey)
      
      // Backend returns: { success: true, message: "...", data: { payment, invoice, customer } }
      if (response?.success) {
        const paymentData = response?.data?.payment || response?.data
        const invoiceData = response?.data?.invoice
        const mode = paymentData?.mode || formData.mode
        const amount = paymentData?.amount || formData.amount
        
        toast.success(`✅ Payment saved: ${amount.toFixed(2)} AED (${mode})`)
        
        if (invoiceData) {
          const status = invoiceData.status || invoiceData.paymentStatus || 'PENDING'
          toast.success(`Invoice ${invoiceData.invoiceNo || ''} status: ${status}`)
        }
        
        onPaymentSuccess?.(response?.data || response)
        onClose()
        
        // Reset form
        setFormData({
          amount: 0,
          mode: 'CASH',
          reference: '',
          paymentDate: new Date().toISOString().split('T')[0]
        })
      } else {
        toast.error(response?.message || 'Failed to save payment')
      }
    } catch (error) {
      console.error('Failed to save payment:', error)
      console.log('❌ Payment error:', {
        message: error?.message,
        responseData: error?.response?.data,
        responseStatus: error?.response?.status,
        fullError: error
      })
      
      // Handle HTTP 409 Conflict (concurrent modification)
      if (error.message?.includes('CONFLICT') || error.response?.status === 409) {
        toast.error('Another user updated this invoice. Please refresh and try again.', {
          duration: 5000,
          icon: '⚠️'
        })
        // Trigger refresh callback if provided
        if (onPaymentSuccess) {
          onPaymentSuccess(null) // Pass null to indicate refresh needed
        }
      } else if (error.response?.data?.errors && Array.isArray(error.response.data.errors)) {
        // Show backend errors array
        const errorMsg = error.response.data.errors.join(', ')
        toast.error(errorMsg)
      } else if (error.response?.data?.message) {
        // Show backend error message
        toast.error(error.response.data.message)
      } else {
        toast.error(error?.message || 'Failed to save payment')
      }
    } finally {
      setLoading(false)
    }
  }

  if (!isOpen) return null

  return (
    <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
      <div className="bg-white rounded-lg shadow-xl max-w-md w-full max-h-[90vh] overflow-y-auto">
        {/* Header */}
        <div className="flex items-center justify-between p-6 border-b">
          <div className="flex items-center gap-2">
            <Wallet className="w-5 h-5 text-green-600" />
            <h2 className="text-xl font-semibold">Make Payment</h2>
          </div>
          <button
            onClick={onClose}
            className="text-gray-400 hover:text-gray-600 transition-colors"
            disabled={loading}
          >
            <X className="w-5 h-5" />
          </button>
        </div>

        {/* Invoice Info */}
        {invoice && (
          <div className="p-6 bg-gray-50 border-b">
            <div className="space-y-2">
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600">Invoice Number:</span>
                <span className="font-medium">{invoice.invoiceNo}</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600">Total Amount:</span>
                <span className="font-medium">{invoice.totalAmount?.toFixed(2) || '0.00'} AED</span>
              </div>
              <div className="flex justify-between items-center">
                <span className="text-sm text-gray-600">Paid So Far:</span>
                <span className="font-medium text-blue-600">{invoice.paidAmount?.toFixed(2) || '0.00'} AED</span>
              </div>
              <div className="flex justify-between items-center pt-2 border-t">
                <span className="text-sm font-semibold text-gray-700">Outstanding:</span>
                <span className="font-bold text-red-600">{invoice.outstandingAmount?.toFixed(2) || '0.00'} AED</span>
              </div>
            </div>
          </div>
        )}

        {/* Form */}
        <form onSubmit={handleSubmit} className="p-6 space-y-4">
          {/* Amount */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              <DollarSign className="w-4 h-4 inline mr-1" />
              Payment Amount *
            </label>
            <input
              type="number"
              step="0.01"
              min="0.01"
              max={invoice?.outstandingAmount || 999999}
              value={formData.amount}
              onChange={(e) => setFormData(prev => ({ ...prev, amount: parseFloat(e.target.value) || 0 }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-green-500"
              required
              disabled={loading}
            />
            {invoice && (
              <p className="mt-1 text-xs text-gray-500">
                Outstanding: {invoice.outstandingAmount?.toFixed(2) || '0.00'} AED
              </p>
            )}
          </div>

          {/* Payment Mode */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              Payment Mode *
            </label>
            <select
              value={formData.mode}
              onChange={(e) => setFormData(prev => ({ ...prev, mode: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-green-500"
              required
              disabled={loading}
            >
              <option value="CASH">Cash</option>
              <option value="CHEQUE">Cheque</option>
              <option value="ONLINE">Online Transfer</option>
              <option value="CREDIT">Credit</option>
            </select>
          </div>

          {/* Reference */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              <FileText className="w-4 h-4 inline mr-1" />
              Reference (Cheque No / Transaction ID)
            </label>
            <input
              type="text"
              value={formData.reference}
              onChange={(e) => setFormData(prev => ({ ...prev, reference: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-green-500"
              placeholder="Enter cheque number or transaction ID"
              maxLength={200}
              disabled={loading}
            />
          </div>

          {/* Payment Date */}
          <div>
            <label className="block text-sm font-medium text-gray-700 mb-2">
              <Calendar className="w-4 h-4 inline mr-1" />
              Payment Date *
            </label>
            <input
              type="date"
              value={formData.paymentDate}
              onChange={(e) => setFormData(prev => ({ ...prev, paymentDate: e.target.value }))}
              className="w-full px-4 py-2 border border-gray-300 rounded-lg focus:ring-2 focus:ring-green-500 focus:border-green-500"
              required
              disabled={loading}
            />
          </div>

          {/* Actions */}
          <div className="flex gap-3 pt-4">
            <button
              type="button"
              onClick={onClose}
              className="flex-1 px-4 py-2 border border-gray-300 rounded-lg text-gray-700 hover:bg-gray-50 transition-colors"
              disabled={loading}
            >
              Cancel
            </button>
            <button
              type="submit"
              className="flex-1 px-4 py-2 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors disabled:opacity-50 disabled:cursor-not-allowed"
              disabled={loading}
            >
              {loading ? 'Saving...' : 'Save Payment'}
            </button>
          </div>
        </form>
      </div>
    </div>
  )
}

export default PaymentModal

