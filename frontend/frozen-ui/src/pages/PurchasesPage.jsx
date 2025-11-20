import { useState, useEffect, useRef } from 'react'
import { Plus, Edit, Trash2, Eye, Save, Search, X } from 'lucide-react'
import { purchasesAPI, productsAPI } from '../services'
import toast from 'react-hot-toast'

const PurchasesPage = () => {
  const [purchases, setPurchases] = useState([])
  const [loading, setLoading] = useState(true)
  const [currentPage, setCurrentPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [showForm, setShowForm] = useState(false)
  const [editingPurchase, setEditingPurchase] = useState(null)
  const [formData, setFormData] = useState({
    supplierName: '',
    invoiceNo: '',
    purchaseDate: new Date().toISOString().split('T')[0],
    expenseCategory: 'Inventory', // Default category
    items: []
  })
  const [products, setProducts] = useState([])
  const [productSearchTerm, setProductSearchTerm] = useState('')
  const [showProductSearch, setShowProductSearch] = useState(false)
  const searchInputRef = useRef(null)

  useEffect(() => {
    loadPurchases()
    loadProducts()
    // Auto-refresh purchases and stock every 60 seconds (reduced frequency)
    // Only refresh if page is visible and form is not open
    const refreshInterval = setInterval(() => {
      if (document.visibilityState === 'visible' && !showForm) {
        loadPurchases()
        loadProducts()
      }
    }, 60000) // 60 seconds - reduced from 20
    
    return () => clearInterval(refreshInterval)
  }, [currentPage])

  useEffect(() => {
    if (showProductSearch && searchInputRef.current) {
      searchInputRef.current.focus()
    }
  }, [showProductSearch])

  const loadPurchases = async () => {
    try {
      setLoading(true)
      const response = await purchasesAPI.getPurchases({ page: currentPage, pageSize: 10 })
      if (response.success) {
        setPurchases(response.data.items)
        setTotalPages(response.data.totalPages)
      }
    } catch (error) {
      toast.error('Failed to load purchases')
    } finally {
      setLoading(false)
    }
  }

  const loadProducts = async () => {
    try {
      const response = await productsAPI.getProducts({ pageSize: 100 })
      if (response.success) {
        setProducts(response.data.items || [])
      }
    } catch (error) {
      console.error('Failed to load products')
    }
  }

  const searchProducts = async (query) => {
    if (!query || query.length < 2) {
      loadProducts()
      return
    }
    try {
      const response = await productsAPI.searchProducts(query, 20)
      if (response.success) {
        setProducts(response.data || [])
      }
    } catch (error) {
      console.error('Failed to search products')
    }
  }

  useEffect(() => {
    const timeoutId = setTimeout(() => {
      if (productSearchTerm) {
        searchProducts(productSearchTerm)
      }
    }, 300)
    return () => clearTimeout(timeoutId)
  }, [productSearchTerm])

  const addItem = (product) => {
    const newItem = {
      productId: product.id,
      productName: product.nameEn,
      sku: product.sku,
      unitType: product.unitType,
      qty: 1,
      unitCost: product.costPrice || 0
    }
    setFormData({
      ...formData,
      items: [...formData.items, newItem]
    })
    setShowProductSearch(false)
    setProductSearchTerm('')
  }

  const updateItem = (index, field, value) => {
    const newItems = [...formData.items]
    
    // Handle empty string for number fields
    const numValue = value === '' ? '' : (field === 'qty' || field === 'unitCost' ? Number(value) : value)
    newItems[index] = { ...newItems[index], [field]: numValue }
    
    setFormData({ ...formData, items: newItems })
  }

  const removeItem = (index) => {
    setFormData({
      ...formData,
      items: formData.items.filter((_, i) => i !== index)
    })
  }

  const calculateTotal = () => {
    return formData.items.reduce((sum, item) => {
      const qty = typeof item.qty === 'number' ? item.qty : 0
      const unitCost = typeof item.unitCost === 'number' ? item.unitCost : 0
      return sum + (qty * unitCost)
    }, 0)
  }

  const handleSubmit = async (e) => {
    e.preventDefault()
    if (formData.items.length === 0) {
      toast.error('Please add at least one item')
      return
    }

    try {
      const purchaseData = {
        supplierName: formData.supplierName,
        invoiceNo: formData.invoiceNo,
        purchaseDate: formData.purchaseDate,
        expenseCategory: formData.expenseCategory, // Include expense category
        items: formData.items.map(item => ({
          productId: item.productId,
          unitType: item.unitType,
          qty: item.qty,
          unitCost: item.unitCost
        }))
      }

      let response
      if (editingPurchase) {
        response = await purchasesAPI.updatePurchase(editingPurchase.id, purchaseData)
        if (response.success) {
          toast.success('Purchase updated successfully!')
        } else {
          toast.error(response.message || 'Failed to update purchase')
        }
      } else {
        response = await purchasesAPI.createPurchase(purchaseData)
        if (response.success) {
          toast.success('Purchase created successfully!')
        } else {
          toast.error(response.message || 'Failed to create purchase')
        }
      }

      if (response.success) {
        setShowForm(false)
        setEditingPurchase(null)
        setFormData({
          supplierName: '',
          invoiceNo: '',
          purchaseDate: new Date().toISOString().split('T')[0],
          expenseCategory: 'Inventory',
          items: []
        })
        loadPurchases()
      }
    } catch (error) {
      toast.error(editingPurchase ? 'Failed to update purchase' : 'Failed to create purchase')
    }
  }

  const handleNewPurchase = () => {
    setEditingPurchase(null)
    setFormData({
      supplierName: '',
      invoiceNo: '',
      purchaseDate: new Date().toISOString().split('T')[0],
      expenseCategory: 'Inventory', // Reset to default
      items: []
    })
    setShowForm(true)
  }

  const handleEditPurchase = (purchase) => {
    setEditingPurchase(purchase)
    setFormData({
      supplierName: purchase.supplierName || '',
      invoiceNo: purchase.invoiceNo || '',
      purchaseDate: purchase.purchaseDate ? new Date(purchase.purchaseDate).toISOString().split('T')[0] : new Date().toISOString().split('T')[0],
      expenseCategory: purchase.expenseCategory || 'Inventory',
      items: purchase.items?.map(item => ({
        productId: item.productId,
        productName: item.productName || item.product?.nameEn || '',
        sku: item.product?.sku || '',
        unitType: item.unitType || 'CRTN',
        qty: item.qty || 0,
        unitCost: item.unitCost || 0
      })) || []
    })
    setShowForm(true)
  }

  // TALLY ERP PURCHASE VOUCHER STYLE
  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-slate-50">
      {/* Top Bar - Mobile Responsive */}
      <div className="bg-blue-100 border-b-2 border-blue-200 px-2 sm:px-4 py-2">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2 sm:gap-0">
          <div>
            <h1 className="text-base sm:text-lg font-bold text-gray-900">Purchase Voucher</h1>
            <div className="text-xs text-gray-600">Date: {new Date().toLocaleDateString('en-GB')}</div>
          </div>
          <button
            onClick={handleNewPurchase}
            className="px-2 sm:px-3 lg:px-4 py-1.5 sm:py-2 bg-green-600 text-white rounded font-medium hover:bg-green-700 flex items-center justify-center text-xs sm:text-sm w-full sm:w-auto"
          >
            <Plus className="h-3.5 w-3.5 sm:h-4 sm:w-4 sm:mr-2" />
            <span className="hidden sm:inline">New Purchase</span>
            <span className="sm:hidden">New</span>
          </button>
        </div>
      </div>

      <div className="p-2 sm:p-4">
        {/* Purchase Form - Tally Style */}
        {showForm && (
          <div className="bg-white rounded-lg border-2 border-lime-300 shadow-lg p-6 mb-6">
            <div className="flex items-center justify-between mb-3 sm:mb-4 border-b-2 border-lime-400 pb-2">
              <h2 className="text-base sm:text-lg font-bold text-gray-900">
                {editingPurchase ? 'Edit Purchase Entry' : 'New Purchase Entry'}
              </h2>
              <button
                onClick={() => setShowForm(false)}
                className="text-gray-500 hover:text-gray-700"
              >
                <X className="h-5 w-5" />
              </button>
            </div>

            <form onSubmit={handleSubmit}>
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-4 gap-2 sm:gap-3 lg:gap-4 mb-4 sm:mb-6">
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Supplier Name *</label>
                  <input
                    type="text"
                    required
                    className="w-full px-3 py-2 border-2 border-lime-300 rounded text-sm"
                    value={formData.supplierName}
                    onChange={(e) => setFormData({ ...formData, supplierName: e.target.value })}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Invoice No *</label>
                  <input
                    type="text"
                    required
                    className="w-full px-3 py-2 border-2 border-lime-300 rounded text-sm"
                    value={formData.invoiceNo}
                    onChange={(e) => setFormData({ ...formData, invoiceNo: e.target.value })}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Purchase Date *</label>
                  <input
                    type="date"
                    required
                    className="w-full px-3 py-2 border-2 border-lime-300 rounded text-sm"
                    value={formData.purchaseDate}
                    onChange={(e) => setFormData({ ...formData, purchaseDate: e.target.value })}
                  />
                </div>
                <div>
                  <label className="block text-sm font-medium text-gray-700 mb-1">Expense Category *</label>
                  <select
                    required
                    className="w-full px-3 py-2 border-2 border-lime-300 rounded text-sm"
                    value={formData.expenseCategory}
                    onChange={(e) => setFormData({ ...formData, expenseCategory: e.target.value })}
                  >
                    <option value="Inventory">Inventory (Stock Items)</option>
                    <option value="Supplies">Supplies (Office/Packaging)</option>
                    <option value="Equipment">Equipment (Machinery/Tools)</option>
                    <option value="Maintenance">Maintenance & Repairs</option>
                    <option value="Other">Other Expenses</option>
                  </select>
                </div>
              </div>

              {/* Product Search */}
              <div className="mb-4">
                <label className="block text-sm font-medium text-gray-700 mb-1">Add Product (F3)</label>
                <div className="relative">
                  <input
                    ref={searchInputRef}
                    type="text"
                    placeholder="Search products..."
                    className="w-full px-3 py-2 border-2 border-lime-300 rounded text-sm"
                    value={productSearchTerm}
                    onChange={(e) => {
                      setProductSearchTerm(e.target.value)
                      setShowProductSearch(true)
                    }}
                    onFocus={() => setShowProductSearch(true)}
                  />
                  <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
                </div>

                {showProductSearch && products.length > 0 && (
                  <div className="absolute z-10 mt-1 w-full max-w-md bg-white border-2 border-lime-300 rounded shadow-lg max-h-64 overflow-y-auto">
                    {products.map((product) => (
                      <div
                        key={product.id}
                        className="p-2 border-b border-lime-200 hover:bg-lime-50 cursor-pointer"
                        onClick={() => addItem(product)}
                      >
                        <div className="flex justify-between">
                          <div>
                            <p className="font-medium text-sm">{product.nameEn}</p>
                            <p className="text-xs text-gray-500">SKU: {product.sku}</p>
                          </div>
                          <div className="text-right">
                            <p className="text-sm font-medium">AED {product.costPrice?.toFixed(2) || '0.00'}</p>
                            <p className="text-xs text-gray-500">Stock: {product.stockQty}</p>
                          </div>
                        </div>
                      </div>
                    ))}
                  </div>
                )}
              </div>

              {/* Items Table - Tally Style */}
              <div className="mb-6">
                <div className="bg-lime-100 p-2 border-b-2 border-lime-400">
                  <h3 className="text-sm font-bold text-gray-900">Items Table</h3>
                </div>
                <div className="overflow-x-auto">
                  <table className="w-full text-xs border-2 border-lime-300">
                    <thead className="bg-lime-100">
                      <tr>
                        <th className="px-2 py-2 border-r border-lime-300 text-left">SL</th>
                        <th className="px-2 py-2 border-r border-lime-300 text-left">Description</th>
                        <th className="px-2 py-2 border-r border-lime-300 text-left">Unit</th>
                        <th className="px-2 py-2 border-r border-lime-300 text-left">Qty</th>
                        <th className="px-2 py-2 border-r border-lime-300 text-left">Unit Cost</th>
                        <th className="px-2 py-2 text-left">Amount</th>
                        <th className="px-2 py-2 text-center">Action</th>
                      </tr>
                    </thead>
                    <tbody className="divide-y divide-lime-200">
                      {formData.items.length === 0 ? (
                        <tr>
                          <td colSpan="7" className="px-4 py-8 text-center text-gray-500">
                            No items. Search and add products.
                          </td>
                        </tr>
                      ) : (
                        formData.items.map((item, index) => (
                          <tr key={index} className="hover:bg-lime-50">
                            <td className="px-2 py-2 border-r border-lime-200 text-center">{index + 1}</td>
                            <td className="px-2 py-2 border-r border-lime-200">
                              <div>
                                <p className="font-medium">{item.productName}</p>
                                <p className="text-gray-500">{item.sku}</p>
                              </div>
                            </td>
                            <td className="px-2 py-2 border-r border-lime-200">
                              <input
                                type="number"
                                min="0"
                                step="0.01"
                                className="w-20 px-1 py-1 border border-lime-300 rounded text-xs"
                                value={item.qty === '' ? '' : item.qty}
                                onChange={(e) => updateItem(index, 'qty', e.target.value)}
                              />
                            </td>
                            <td className="px-2 py-2 border-r border-lime-200">
                              <select
                                className="w-full px-1 py-1 border border-lime-300 rounded text-xs uppercase"
                                value={item.unitType || 'CRTN'}
                                onChange={(e) => updateItem(index, 'unitType', e.target.value)}
                              >
                                <option value="CRTN">CRTN</option>
                                <option value="KG">KG</option>
                                <option value="PIECE">PIECE</option>
                                <option value="BOX">BOX</option>
                                <option value="PKG">PKG</option>
                                <option value="BAG">BAG</option>
                                <option value="PC">PC</option>
                                <option value="UNIT">UNIT</option>
                                <option value="CTN">CTN</option>
                                <option value="PCS">PCS</option>
                                <option value="LTR">LTR</option>
                                <option value="MTR">MTR</option>
                              </select>
                            </td>
                            <td className="px-2 py-2 border-r border-lime-200">
                              <input
                                type="number"
                                min="0"
                                step="0.01"
                                className="w-20 px-1 py-1 border border-lime-300 rounded text-xs"
                                value={item.unitCost === '' ? '' : item.unitCost}
                                onChange={(e) => updateItem(index, 'unitCost', e.target.value)}
                              />
                            </td>
                            <td className="px-2 py-2 font-medium">
                              AED {(() => {
                                const qty = typeof item.qty === 'number' ? item.qty : 0
                                const cost = typeof item.unitCost === 'number' ? item.unitCost : 0
                                return (qty * cost).toFixed(2)
                              })()}
                            </td>
                            <td className="px-2 py-2 text-center">
                              <button
                                type="button"
                                onClick={() => removeItem(index)}
                                className="text-red-600 hover:text-red-800"
                              >
                                <Trash2 className="h-4 w-4" />
                              </button>
                            </td>
                          </tr>
                        ))
                      )}
                    </tbody>
                    <tfoot className="bg-lime-100">
                      <tr>
                        <td colSpan="5" className="px-2 py-2 text-right font-bold border-r border-lime-300">Total:</td>
                        <td className="px-2 py-2 font-bold text-green-700">AED {calculateTotal().toFixed(2)}</td>
                        <td></td>
                      </tr>
                    </tfoot>
                  </table>
                </div>
              </div>

              <div className="flex justify-end space-x-3">
                <button
                  type="button"
                  onClick={() => setShowForm(false)}
                  className="px-4 py-2 border-2 border-lime-300 rounded text-sm font-medium hover:bg-lime-50"
                >
                  Cancel
                </button>
                <button
                  type="submit"
                  className="px-4 py-2 bg-green-600 text-white rounded text-sm font-medium hover:bg-green-700 flex items-center"
                >
                  <Save className="h-4 w-4 mr-2" />
                  Save Purchase
                </button>
              </div>
            </form>
          </div>
        )}

        {/* Purchases List - Tally Style */}
        <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm">
          <div className="p-4 border-b-2 border-lime-400 bg-lime-100">
            <h3 className="text-sm font-bold text-gray-900">Purchase List</h3>
          </div>
          {loading ? (
            <div className="flex items-center justify-center h-64">
              <div className="animate-spin rounded-full h-12 w-12 border-b-2 border-green-600"></div>
            </div>
          ) : (
            <div className="overflow-x-auto">
              <table className="w-full text-xs">
                <thead className="bg-lime-100">
                  <tr>
                    <th className="px-3 py-2 border-r border-lime-300 text-left">Invoice No</th>
                    <th className="px-3 py-2 border-r border-lime-300 text-left">Supplier</th>
                    <th className="px-3 py-2 border-r border-lime-300 text-left">Date</th>
                    <th className="px-3 py-2 border-r border-lime-300 text-right">Amount</th>
                    <th className="px-3 py-2 border-r border-lime-300 text-center">Items</th>
                    <th className="px-3 py-2 text-center">Actions</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-lime-200">
                  {purchases.length === 0 ? (
                    <tr>
                      <td colSpan="6" className="px-4 py-8 text-center text-gray-500">
                        No purchases found. Create a new purchase.
                      </td>
                    </tr>
                  ) : (
                    purchases.map((purchase) => (
                      <tr key={purchase.id} className="hover:bg-lime-50">
                        <td className="px-3 py-2 font-medium">{purchase.invoiceNo}</td>
                        <td className="px-3 py-2">{purchase.supplierName}</td>
                        <td className="px-3 py-2">{new Date(purchase.purchaseDate).toLocaleDateString('en-GB')}</td>
                        <td className="px-3 py-2 text-right font-medium">AED {purchase.totalAmount.toFixed(2)}</td>
                        <td className="px-3 py-2 text-center">{purchase.items?.length || 0}</td>
                        <td className="px-3 py-2">
                          <div className="flex justify-center space-x-2">
                            <button 
                              onClick={() => handleEditPurchase(purchase)}
                              className="bg-blue-50 text-blue-600 hover:bg-blue-600 hover:text-white border border-blue-300 px-2 py-1 rounded text-xs font-medium transition-colors flex items-center gap-1"
                              title="Edit Purchase"
                            >
                              <Edit className="h-3.5 w-3.5" />
                              Edit
                            </button>
                            <button className="bg-indigo-50 text-indigo-600 hover:bg-indigo-600 hover:text-white border border-indigo-300 px-2 py-1 rounded text-xs font-medium transition-colors flex items-center gap-1"
                              title="View Purchase"
                            >
                              <Eye className="h-3.5 w-3.5" />
                              View
                            </button>
                          </div>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          )}
          
          {totalPages > 1 && (
            <div className="p-4 border-t border-lime-300 flex justify-center space-x-2">
              <button
                onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
                disabled={currentPage === 1}
                className="px-3 py-1 border border-lime-300 rounded text-xs disabled:opacity-50"
              >
                Previous
              </button>
              <span className="px-4 py-1 text-xs">
                Page {currentPage} of {totalPages}
              </span>
              <button
                onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}
                disabled={currentPage === totalPages}
                className="px-3 py-1 border border-lime-300 rounded text-xs disabled:opacity-50"
              >
                Next
              </button>
            </div>
          )}
        </div>
      </div>
    </div>
  )
}

export default PurchasesPage
