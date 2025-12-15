import api from './api'

export const authAPI = {
  login: async (credentials) => {
    const response = await api.post('/auth/login', credentials)
    return response.data
  },

  register: async (userData) => {
    const response = await api.post('/auth/register', userData)
    return response.data
  },

  forgotPassword: async (email) => {
    const response = await api.post('/auth/forgot', { email })
    return response.data
  },

  validateToken: async () => {
    const response = await api.get('/auth/validate')
    return response.data
  },
}

export const productsAPI = {
  getProducts: async (params = {}) => {
    const response = await api.get('/products', { params })
    return response.data
  },

  getProduct: async (id) => {
    const response = await api.get(`/products/${id}`)
    return response.data
  },

  createProduct: async (product) => {
    const response = await api.post('/products', product)
    return response.data
  },

  updateProduct: async (id, product) => {
    const response = await api.put(`/products/${id}`, product)
    return response.data
  },

  deleteProduct: async (id) => {
    const response = await api.delete(`/products/${id}`)
    return response.data
  },

  adjustStock: async (id, adjustment) => {
    const response = await api.post(`/products/${id}/adjust-stock`, adjustment)
    return response.data
  },

  getLowStockProducts: async () => {
    const response = await api.get('/products/low-stock')
    return response.data
  },

  searchProducts: async (query, limit = 20) => {
    const response = await api.get('/products/search', { params: { q: query, limit } })
    return response.data
  },

  importExcel: async (file) => {
    const formData = new FormData()
    formData.append('file', file)
    const response = await api.post('/products/import-excel', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },

  resetAllStock: async () => {
    const response = await api.post('/products/reset-all-stock')
    return response.data
  },
}

export const salesAPI = {
  getSales: async (params = {}) => {
    const response = await api.get('/sales', { params })
    return response.data
  },

  getSale: async (id) => {
    const response = await api.get(`/sales/${id}`)
    return response.data
  },

  createSale: async (sale) => {
    const response = await api.post('/sales', sale)
    return response.data
  },

  createSaleWithOverride: async (sale, reason) => {
    const response = await api.post('/sales/override', { saleRequest: sale, reason })
    return response.data
  },
  updateSale: async (id, sale) => {
    const response = await api.put(`/sales/${id}`, sale)
    return response.data
  },
  unlockInvoice: async (id, reason) => {
    const response = await api.post(`/sales/${id}/unlock`, { reason })
    return response.data
  },
  deleteSale: async (id) => {
    const response = await api.delete(`/sales/${id}`)
    return response.data
  },

  getInvoicePdf: async (id) => {
    try {
      const response = await api.get(`/sales/${id}/pdf`, { 
        responseType: 'blob'
      })
      
      // Check content type from response headers
      const contentType = response.headers['content-type'] || ''
      
      // If response status is error or content-type is JSON, it's an error
      if (response.status >= 400 || contentType.includes('application/json')) {
        // Response is an error - try to parse JSON from blob
        const text = await response.data.text()
        try {
          const errorData = JSON.parse(text)
          const errorMessage = errorData?.message || errorData?.errors?.join(', ') || 'Failed to generate PDF'
          throw new Error(errorMessage)
        } catch (parseError) {
          throw new Error(`Server error: ${response.status}`)
        }
      }
      
      // Valid PDF blob
      return response.data
    } catch (error) {
      // If it's an axios error with response
      if (error.response) {
        const contentType = error.response.headers['content-type'] || ''
        
        // If error response is JSON (axios auto-parsed it)
        if (contentType.includes('application/json')) {
          const errorData = error.response.data
          const errorMessage = errorData?.message || errorData?.errors?.join(', ') || 'Failed to generate PDF'
          throw new Error(errorMessage)
        }
        
        // If error response is blob (might be JSON error wrapped as blob)
        if (error.response.data instanceof Blob) {
          try {
            // Read blob as text to check if it's JSON error
            const text = await error.response.data.text()
            if (text.trim().startsWith('{')) {
              const errorData = JSON.parse(text)
              throw new Error(errorData.message || errorData.errors?.join(', ') || 'Failed to generate PDF')
            }
            throw new Error(`Server error: ${error.response.status}`)
          } catch (parseError) {
            if (parseError.message) {
              throw parseError
            }
            throw new Error(`Server error: ${error.response.status}`)
          }
        }
      }
      
      // Re-throw with message
      throw new Error(error.message || 'Failed to generate PDF')
    }
  },

  sendInvoiceEmail: async (id, email) => {
    const response = await api.post(`/sales/${id}/email`, { email })
    return response.data
  },

  getCombinedInvoicesPdf: async (invoiceIds) => {
    try {
      const response = await api.post(`/sales/combined-pdf`, 
        { invoiceIds },
        { responseType: 'blob' }
      )
      
      const contentType = response.headers['content-type'] || ''
      
      if (response.status >= 400 || contentType.includes('application/json')) {
        const text = await response.data.text()
        try {
          const errorData = JSON.parse(text)
          const errorMessage = errorData?.message || errorData?.errors?.join(', ') || 'Failed to generate combined PDF'
          throw new Error(errorMessage)
        } catch (parseError) {
          throw new Error(`Server error: ${response.status}`)
        }
      }
      
      return response.data
    } catch (error) {
      if (error.response) {
        const contentType = error.response.headers['content-type'] || ''
        
        if (contentType.includes('application/json')) {
          const errorData = error.response.data
          const errorMessage = errorData?.message || errorData?.errors?.join(', ') || 'Failed to generate combined PDF'
          throw new Error(errorMessage)
        }
        
        if (error.response.data instanceof Blob) {
          try {
            const text = await error.response.data.text()
            if (text.trim().startsWith('{')) {
              const errorData = JSON.parse(text)
              throw new Error(errorData.message || errorData.errors?.join(', ') || 'Failed to generate combined PDF')
            }
            throw new Error(`Server error: ${error.response.status}`)
          } catch (parseError) {
            if (parseError.message) {
              throw parseError
            }
            throw new Error(`Server error: ${error.response.status}`)
          }
        }
      }
      
      throw new Error(error.message || 'Failed to generate combined PDF')
    }
  },

  getNextInvoiceNumber: async () => {
    const response = await api.get('/sales/next-invoice-number')
    return response.data
  },

  validateInvoiceNumber: async (invoiceNumber, excludeSaleId = null) => {
    const response = await api.post('/sales/validate-invoice-number', {
      invoiceNumber,
      excludeSaleId
    })
    return response.data
  },
}

export const purchasesAPI = {
  getPurchases: async (params = {}) => {
    const response = await api.get('/purchases', { params })
    return response.data
  },

  getPurchaseAnalytics: async (params = {}) => {
    const response = await api.get('/purchases/analytics', { params })
    return response.data
  },

  getPurchase: async (id) => {
    const response = await api.get(`/purchases/${id}`)
    return response.data
  },

  createPurchase: async (purchase) => {
    const response = await api.post('/purchases', purchase)
    return response.data
  },

  updatePurchase: async (id, purchase) => {
    const response = await api.put(`/purchases/${id}`, purchase)
    return response.data
  },

  deletePurchase: async (id) => {
    const response = await api.delete(`/purchases/${id}`)
    return response.data
  },
}

export const customersAPI = {
  getCustomers: async (params = {}) => {
    const response = await api.get('/customers', { params })
    return response.data
  },

  searchCustomers: async (query, limit = 20) => {
    const response = await api.get('/customers/search', { params: { q: query, limit } })
    return response.data
  },

  getCustomer: async (id) => {
    const response = await api.get(`/customers/${id}`)
    return response.data
  },

  createCustomer: async (customer) => {
    const response = await api.post('/customers', customer)
    return response.data
  },

  updateCustomer: async (id, customer) => {
    const response = await api.put(`/customers/${id}`, customer)
    return response.data
  },

  deleteCustomer: async (id, forceDelete = false) => {
    const response = await api.delete(`/customers/${id}`, {
      params: { forceDelete }
    })
    return response.data
  },

  getCustomerLedger: async (id) => {
    const response = await api.get(`/customers/${id}/ledger`)
    return response.data
  },

  getCashCustomerLedger: async () => {
    const response = await api.get('/customers/cash-customer/ledger')
    return response.data
  },

  getOutstandingInvoices: async (id) => {
    const response = await api.get(`/customers/${id}/outstanding-invoices`)
    return response.data
  },

  recalculateBalance: async (id) => {
    const response = await api.post(`/customers/${id}/recalculate-balance`)
    return response.data
  },

  getCustomerStatement: async (id, fromDate, toDate) => {
    const response = await api.get(`/customers/${id}/statement`, {
      params: { fromDate, toDate },
      responseType: 'blob'
    })
    return response.data
  },

  getCustomerPendingBillsPdf: async (id, fromDate, toDate) => {
    const params = {}
    if (fromDate) params.fromDate = fromDate
    if (toDate) params.toDate = toDate
    const response = await api.get(`/customers/${id}/pending-bills-pdf`, {
      params,
      responseType: 'blob'
    })
    return response.data
  },
}

export const paymentsAPI = {
  getPayments: async (params = {}) => {
    const response = await api.get('/payments', { params })
    return response.data
  },

  getPayment: async (id) => {
    const response = await api.get(`/payments/${id}`)
    return response.data
  },

  createPayment: async (payment, idempotencyKey = null) => {
    const headers = {}
    // Generate idempotency key if not provided (for duplicate prevention)
    const key = idempotencyKey || crypto.randomUUID()
    headers['Idempotency-Key'] = key
    
    try {
      const response = await api.post('/payments', payment, { headers })
      return response.data
    } catch (error) {
      // Handle HTTP 409 Conflict (concurrent modification)
      if (error.response?.status === 409) {
        throw new Error('CONFLICT: Another user updated this invoice. Please refresh and try again.')
      }
      throw error
    }
  },

  updateChequeStatus: async (id, status) => {
    const response = await api.put(`/payments/${id}/cheque-status`, { status })
    return response.data
  },

  updatePaymentStatus: async (id, status) => {
    const response = await api.put(`/payments/${id}/status`, { status })
    return response.data
  },

  getOutstandingInvoices: async (customerId) => {
    const response = await api.get(`/payments/customers/${customerId}/outstanding-invoices`)
    return response.data
  },

  getInvoiceAmount: async (invoiceId) => {
    const response = await api.get(`/payments/invoices/${invoiceId}/amount`)
    return response.data
  },

  allocatePayment: async (allocation) => {
    const response = await api.post('/payments/allocate', allocation)
    return response.data
  },

  updatePayment: async (id, paymentData) => {
    const response = await api.put(`/payments/${id}`, paymentData)
    return response.data
  },

  deletePayment: async (id) => {
    const response = await api.delete(`/payments/${id}`)
    return response.data
  },
}

export const expensesAPI = {
  getExpenses: async (params = {}) => {
    const response = await api.get('/expenses', { params })
    return response.data
  },

  getExpense: async (id) => {
    const response = await api.get(`/expenses/${id}`)
    return response.data
  },

  createExpense: async (expense) => {
    const response = await api.post('/expenses', expense)
    return response.data
  },

  updateExpense: async (id, expense) => {
    const response = await api.put(`/expenses/${id}`, expense)
    return response.data
  },

  deleteExpense: async (id) => {
    const response = await api.delete(`/expenses/${id}`)
    return response.data
  },

  getExpenseCategories: async () => {
    const response = await api.get('/expenses/categories')
    return response.data
  },

  getExpensesAggregated: async (params = {}) => {
    const response = await api.get('/expenses/aggregated', { params })
    return response.data
  },

  createCategory: async (categoryData) => {
    const response = await api.post('/expenses/categories', categoryData)
    return response.data
  },
}

export const reportsAPI = {
  getSummaryReport: async (params = {}) => {
    const response = await api.get('/reports/summary', { params })
    return response.data
  },

  getSalesReport: async (params = {}) => {
    const response = await api.get('/reports/sales', { params })
    return response.data
  },
  getEnhancedSalesReport: async (params = {}) => {
    const response = await api.get('/reports/sales-enhanced', { params })
    return response.data
  },
  getProductSalesReport: async (params = {}) => {
    const response = await api.get('/reports/product-sales', { params })
    return response.data
  },
  getEnhancedProductSalesReport: async (params = {}) => {
    const response = await api.get('/reports/products-enhanced', { params })
    return response.data
  },
  getOutstandingCustomers: async (params = {}) => {
    const response = await api.get('/reports/outstanding', { params })
    return response.data
  },
  getCustomerReport: async (params = {}) => {
    const response = await api.get('/reports/customers-enhanced', { params })
    return response.data
  },
  getAgingReport: async (params = {}) => {
    const response = await api.get('/reports/aging', { params })
    return response.data
  },
  getStockReport: async (params = {}) => {
    const response = await api.get('/reports/stock', { params })
    return response.data
  },
  getComprehensiveSalesLedger: async (params = {}) => {
    const response = await api.get('/reports/sales-ledger', { params })
    return response.data
  },

  getChequeReport: async () => {
    const response = await api.get('/reports/cheque')
    return response.data
  },

  getAISuggestions: async (params = {}) => {
    const response = await api.get('/reports/ai-suggestions', { params })
    return response.data
  },

  getPendingBills: async (params = {}) => {
    const response = await api.get('/reports/pending', { params })
    return response.data
  },

  exportPendingBillsPdf: async (params = {}) => {
    const response = await api.get('/reports/pending-bills/export/pdf', { params, responseType: 'blob' })
    return response.data
  },

  getExpensesByCategory: async (params = {}) => {
    const response = await api.get('/reports/expenses', { params })
    return response.data
  },

  getSalesVsExpenses: async (params = {}) => {
    const response = await api.get('/reports/sales-vs-expenses', { params })
    return response.data
  },

  exportReportPdf: async (params = {}) => {
    const response = await api.get('/reports/export/pdf', { params, responseType: 'blob' })
    return response.data
  },

  exportReportExcel: async (params = {}) => {
    const response = await api.get('/reports/export/excel', { params, responseType: 'blob' })
    return response.data
  },

  exportReportCsv: async (params = {}) => {
    const response = await api.get('/reports/export/csv', { params, responseType: 'blob' })
    return response.data
  },
}

export const adminAPI = {
  getSettings: async () => {
    const response = await api.get('/admin/settings')
    return response.data
  },

  updateSettings: async (settings) => {
    const response = await api.put('/admin/settings', settings)
    return response.data
  },

  uploadLogo: async (file) => {
    const formData = new FormData()
    formData.append('file', file)
    const response = await api.post('/admin/logo/upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },

  deleteLogo: async () => {
    const response = await api.delete('/admin/logo')
    return response.data
  },

  createBackup: async () => {
    const response = await api.post('/admin/backup')
    return response.data
  },

  createFullBackup: async (exportToDesktop = false) => {
    const response = await api.post(`/admin/backup/full?exportToDesktop=${exportToDesktop}`)
    return response.data
  },

  getBackups: async () => {
    const response = await api.get('/admin/backups')
    return response.data
  },

  getBackupList: async () => {
    const response = await api.get('/admin/backup/list')
    return response.data
  },

  downloadBackup: async (fileName) => {
    const response = await api.get(`/admin/backup/download/${fileName}`, { responseType: 'blob' })
    return response.data
  },

  restoreBackup: async (fileName) => {
    const response = await api.post('/admin/backup/restore', { fileName })
    return response.data
  },

  restoreBackupFromUpload: async (file) => {
    const formData = new FormData()
    formData.append('file', file)
    const response = await api.post('/admin/backup/restore-upload', formData, {
      headers: { 'Content-Type': 'multipart/form-data' }
    })
    return response.data
  },

  deleteBackup: async (fileName) => {
    const response = await api.delete(`/admin/backup/${fileName}`)
    return response.data
  },

  emailBackup: async (fileName, email) => {
    const response = await api.post('/admin/backup/email', { fileName, email })
    return response.data
  },

  getAuditLogs: async (params = {}) => {
    const response = await api.get('/admin/audit-logs', { params })
    return response.data
  },

  getUsers: async (params = {}) => {
    const response = await api.get('/admin/users', { params })
    return response.data
  },

  createUser: async (userData) => {
    const response = await api.post('/admin/users', userData)
    return response.data
  },

  updateUser: async (id, userData) => {
    const response = await api.put(`/admin/users/${id}`, userData)
    return response.data
  },

  resetPassword: async (id, passwordData) => {
    const response = await api.post(`/admin/users/${id}/reset-password`, passwordData)
    return response.data
  },
}

// Alerts API
export const alertsAPI = {
  getAlerts: async (params = {}) => {
    const response = await api.get('/alerts', { params })
    return response.data
  },
  getUnreadCount: async () => {
    const response = await api.get('/alerts/unread-count')
    return response.data
  },
  markAsRead: async (id) => {
    const response = await api.post(`/alerts/${id}/read`)
    return response.data
  },
  markAsResolved: async (id) => {
    const response = await api.post(`/alerts/${id}/resolve`)
    return response.data
  }
}

// Validation API (Admin only)
export const validationAPI = {
  validateCustomer: async (customerId) => {
    const response = await api.get(`/validation/customer/${customerId}`)
    return response.data
  },
  detectMismatches: async () => {
    const response = await api.get('/validation/detect-mismatches')
    return response.data
  },
  fixCustomer: async (customerId) => {
    const response = await api.post(`/validation/fix-customer/${customerId}`)
    return response.data
  },
  fixAll: async () => {
    const response = await api.post('/validation/fix-all')
    return response.data
  },
  recalculateCustomer: async (customerId) => {
    const response = await api.post(`/validation/recalculate/${customerId}`)
    return response.data
  }
}

// Returns API
export const returnsAPI = {
  createSaleReturn: async (data) => {
    const response = await api.post('/returns/sales', data)
    return response.data
  },
  createPurchaseReturn: async (data) => {
    const response = await api.post('/returns/purchases', data)
    return response.data
  },
  getSaleReturns: async (saleId = null) => {
    const params = saleId ? { saleId } : {}
    const response = await api.get('/returns/sales', { params })
    return response.data
  },
  getPurchaseReturns: async (purchaseId = null) => {
    const params = purchaseId ? { purchaseId } : {}
    const response = await api.get('/returns/purchases', { params })
    return response.data
  }
}

// Profit API
export const profitAPI = {
  getProfitReport: async (fromDate, toDate) => {
    const response = await api.get('/profit/report', {
      params: { fromDate, toDate }
    })
    return response.data
  },
  getProductProfit: async (fromDate, toDate) => {
    const response = await api.get('/profit/products', {
      params: { fromDate, toDate }
    })
    return response.data
  },
  getDailyProfit: async (date) => {
    const response = await api.get('/profit/daily', {
      params: { date }
    })
    return response.data
  }
}

// Stock Adjustments API
export const stockAdjustmentsAPI = {
  createAdjustment: async (data) => {
    const response = await api.post('/stockadjustments', data)
    return response.data
  },
  getAdjustments: async (productId = null, fromDate = null, toDate = null) => {
    const params = {}
    if (productId) params.productId = productId
    if (fromDate) params.fromDate = fromDate
    if (toDate) params.toDate = toDate
    const response = await api.get('/stockadjustments', { params })
    return response.data
  }
}

// Suppliers API
export const suppliersAPI = {
  getSupplierBalance: async (supplierName) => {
    const response = await api.get(`/suppliers/balance/${encodeURIComponent(supplierName)}`)
    return response.data
  },
  getSupplierTransactions: async (supplierName, fromDate = null, toDate = null) => {
    const params = {}
    if (fromDate) params.fromDate = fromDate
    if (toDate) params.toDate = toDate
    const response = await api.get(`/suppliers/transactions/${encodeURIComponent(supplierName)}`, { params })
    return response.data
  },
  getAllSuppliersSummary: async () => {
    const response = await api.get('/suppliers/summary')
    return response.data
  }
}

// Backup API
export const backupAPI = {
  createBackup: async (exportToDesktop = true, uploadToGoogleDrive = false, sendEmail = false) => {
    const response = await api.post('/backup/create', null, {
      params: { exportToDesktop, uploadToGoogleDrive, sendEmail }
    })
    return response.data
  },
  createFullBackup: async (exportToDesktop = false) => {
    const response = await api.post('/backup/create', null, {
      params: { exportToDesktop, uploadToGoogleDrive: false, sendEmail: false }
    })
    return response.data
  },
  getBackups: async () => {
    const response = await api.get('/backup/list')
    return response.data
  },
  restoreBackup: async (fileName, uploadedFilePath = null) => {
    const response = await api.post('/backup/restore', {
      fileName,
      uploadedFilePath
    })
    return response.data
  },
  restoreBackupFromFile: async (file) => {
    const formData = new FormData()
    formData.append('file', file)
    const response = await api.post('/backup/restore-upload', formData, {
      headers: {
        'Content-Type': 'multipart/form-data'
      }
    })
    return response.data
  },
  deleteBackup: async (fileName) => {
    const response = await api.delete(`/backup/${encodeURIComponent(fileName)}`)
    return response.data
  },
  downloadBackup: async (fileName) => {
    const response = await api.get(`/backup/download/${encodeURIComponent(fileName)}`, {
      responseType: 'blob'
    })
    return response.data
  }
}

// Reset API
export const resetAPI = {
  getSystemSummary: async () => {
    const response = await api.get('/reset/summary')
    return response.data
  },
  executeReset: async (createBackup, clearAuditLogs, confirmationText) => {
    const response = await api.post('/reset/execute', {
      createBackup,
      clearAuditLogs,
      confirmationText
    })
    return response.data
  }
}
