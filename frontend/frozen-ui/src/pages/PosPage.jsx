import { useState, useEffect, useRef, useCallback } from 'react'
import { useNavigate, useSearchParams } from 'react-router-dom'
import { 
  Search, 
  Plus, 
  Minus, 
  Trash2, 
  Save, 
  Printer, 
  User,
  Calculator,
  AlertTriangle,
  X,
  ChevronDown,
  MessageCircle,
  Mail,
  Download,
  CheckCircle
} from 'lucide-react'
import { productsAPI, salesAPI, customersAPI } from '../services'
import { formatCurrency, formatBalance, formatBalanceWithColor } from '../utils/currency'
import { useAuth } from '../hooks/useAuth'
import toast from 'react-hot-toast'

const API_BASE_URL = import.meta.env.VITE_API_BASE_URL || 'http://localhost:5000/api'

const PosPage = () => {
  const navigate = useNavigate()
  const { user } = useAuth()
  const [searchParams, setSearchParams] = useSearchParams()
  const [products, setProducts] = useState([])
  const [customers, setCustomers] = useState([])
  const [cart, setCart] = useState([])
  const [selectedCustomer, setSelectedCustomer] = useState(null)
  const [customerSearchTerm, setCustomerSearchTerm] = useState('')
  const [showCustomerSearch, setShowCustomerSearch] = useState(false)
  const [showProductDropdown, setShowProductDropdown] = useState({})
  const [productSearchTerms, setProductSearchTerms] = useState({}) // Search term for each row
  const [paymentMethod, setPaymentMethod] = useState('Cash')
  const [paymentAmount, setPaymentAmount] = useState('')
  const [notes, setNotes] = useState('')
  const [discount, setDiscount] = useState(0)
  const [discountInput, setDiscountInput] = useState('')
  const [loading, setLoading] = useState(false)
  const [showInvoiceOptionsModal, setShowInvoiceOptionsModal] = useState(false)
  const [lastCreatedInvoice, setLastCreatedInvoice] = useState(null)
  const [nextInvoiceNumber, setNextInvoiceNumber] = useState('0001')
  const [isEditMode, setIsEditMode] = useState(false)
  const [editingSaleId, setEditingSaleId] = useState(null)
  const [editingSale, setEditingSale] = useState(null)
  const [loadingSale, setLoadingSale] = useState(false)
  const [editReason, setEditReason] = useState('')
  const [showEditReasonModal, setShowEditReasonModal] = useState(false)
  const [invoiceDate, setInvoiceDate] = useState(() => {
    const today = new Date()
    return today.toISOString().split('T')[0] // YYYY-MM-DD format
  })
  
  const customerInputRef = useRef(null)
  const productSearchRefs = useRef({})

  // Define loadProducts before useEffect
  const loadProducts = useCallback(async () => {
    try {
      const response = await productsAPI.getProducts({ pageSize: 200 })
      if (response.success) {
        setProducts(response.data.items || [])
      }
    } catch (error) {
      toast.error('Failed to load products')
    }
  }, [])

  const loadCustomers = useCallback(async () => {
    try {
      const response = await customersAPI.getCustomers({ pageSize: 100 })
      if (response.success) {
        setCustomers(response.data.items)
      }
    } catch (error) {
      toast.error('Failed to load customers')
    }
  }, [])

  // Load next invoice number when customer is selected
  const loadNextInvoiceNumber = useCallback(async () => {
    try {
      const response = await salesAPI.getNextInvoiceNumber()
      if (response.success && response.data) {
        const invoiceNo = response.data.invoiceNo || response.data
        // Format as 4-digit number if needed
        const formatted = invoiceNo.length === 1 ? invoiceNo.padStart(4, '0') : invoiceNo
        setNextInvoiceNumber(formatted)
      }
    } catch (error) {
      console.error('Failed to load invoice number:', error)
    }
  }, [])

  // Load sale for editing
  const loadSaleForEdit = useCallback(async (saleId) => {
    try {
      setLoadingSale(true)
      const response = await salesAPI.getSale(saleId)
      if (response.success && response.data) {
        const sale = response.data
        setIsEditMode(true)
        setEditingSaleId(saleId)
        setEditingSale(sale) // Store the full sale object

        // Set customer - try to find in customers array, or create temporary customer object
        if (sale.customerId) {
          if (customers.length > 0) {
            const customer = customers.find(c => c.id === sale.customerId)
            if (customer) {
              setSelectedCustomer(customer)
            } else {
              // Customer not found in list, create temporary customer object
              setSelectedCustomer({
                id: sale.customerId,
                name: sale.customerName || 'Unknown Customer',
                phone: '',
                email: '',
                address: ''
              })
            }
          } else {
            // Customers not loaded yet, create temporary customer object
            // Will be updated when customers load
            setSelectedCustomer({
              id: sale.customerId,
              name: sale.customerName || 'Unknown Customer',
              phone: '',
              email: '',
              address: ''
            })
          }
        }

        // Set discount and notes
        if (sale.discount) {
          setDiscount(sale.discount)
          setDiscountInput(sale.discount.toString())
        } else {
          setDiscountInput('')
        }
        if (sale.notes) setNotes(sale.notes)

        // Load cart items from sale
        if (sale.items && sale.items.length > 0) {
          const cartItems = sale.items.map(item => ({
            productId: item.productId,
            productName: item.productName || '',
            unitType: item.unitType || '',
            qty: item.qty || 0,
            unitPrice: item.unitPrice || 0,
            vatAmount: item.vatAmount || 0,
            lineTotal: item.lineTotal || 0
          }))
          setCart(cartItems)
        }

        // Set payment info if exists
        if (sale.payments && sale.payments.length > 0) {
          const payment = sale.payments[0]
          setPaymentMethod(payment.method || 'Cash')
          setPaymentAmount(payment.amount?.toString() || '')
        }

        // Load invoice date from sale
        if (sale.invoiceDate) {
          const date = new Date(sale.invoiceDate)
          setInvoiceDate(date.toISOString().split('T')[0])
        }

        toast.success(`Invoice ${sale.invoiceNo || saleId} loaded for editing`)
      } else {
        toast.error(response.message || 'Failed to load invoice')
        // Clear edit mode if failed
        setIsEditMode(false)
        setEditingSaleId(null)
        setSearchParams({}) // Clear URL param
      }
    } catch (error) {
      console.error('Failed to load sale for edit:', error)
      toast.error(error?.response?.data?.message || 'Failed to load invoice for editing')
      setIsEditMode(false)
      setEditingSaleId(null)
      setSearchParams({}) // Clear URL param
    } finally {
      setLoadingSale(false)
    }
  }, [customers, setSearchParams])

  useEffect(() => {
    loadProducts()
    loadCustomers()
    
    // Auto-refresh products and customers every 60 seconds (reduced frequency)
    // Only refresh if page is visible and not in edit mode
    const refreshInterval = setInterval(() => {
      if (document.visibilityState === 'visible' && !isEditMode && !loading) {
        loadProducts()
        loadCustomers()
      }
    }, 60000) // 60 seconds - reduced from 15
    
    // Click outside handler for product dropdowns - use mousedown to prevent conflicts
    const handleClickOutside = (e) => {
      // Only close if clicking outside the dropdown container
      const dropdownContainer = e.target.closest('.product-dropdown-container')
      if (!dropdownContainer) {
        setShowProductDropdown({})
        // Clear search terms when clicking outside
        setProductSearchTerms({})
      }
    }

    // Auto-refresh when page becomes visible (user returns from other tab/window)
    const handleVisibilityChange = () => {
      if (!document.hidden) {
        loadProducts()
        loadCustomers()
      }
    }

    // Use mousedown instead of click to avoid conflicts with onClick handlers
    document.addEventListener('mousedown', handleClickOutside)
    document.addEventListener('visibilitychange', handleVisibilityChange)
    
    return () => {
      clearInterval(refreshInterval)
      document.removeEventListener('mousedown', handleClickOutside)
      document.removeEventListener('visibilitychange', handleVisibilityChange)
    }
  }, [loadProducts, loadCustomers])

  // Check for editId in URL - load sale even if customers aren't loaded yet
  useEffect(() => {
    const editIdParam = searchParams.get('editId')
    if (editIdParam && !isEditMode && !loadingSale) {
      const saleId = parseInt(editIdParam)
      if (saleId && !isNaN(saleId)) {
        loadSaleForEdit(saleId)
      }
    }
  }, [searchParams, isEditMode, loadingSale, loadSaleForEdit])

  // Update customer when customers are loaded and we're in edit mode
  useEffect(() => {
    if (isEditMode && editingSale && editingSale.customerId && customers.length > 0) {
      const customer = customers.find(c => c.id === editingSale.customerId)
      if (customer && (!selectedCustomer || selectedCustomer.id !== customer.id)) {
        setSelectedCustomer(customer)
      }
    }
  }, [customers, isEditMode, editingSale, selectedCustomer])

  const filteredCustomers = customers.filter(customer =>
    customer.name?.toLowerCase().includes(customerSearchTerm.toLowerCase()) ||
    customer.phone?.includes(customerSearchTerm)
  )

  // Filter products based on search term for each row
  const getFilteredProducts = (rowIndex) => {
    const searchTerm = productSearchTerms[rowIndex] || ''
    if (!searchTerm.trim()) {
      // Show all products when no search (or first 50 for better performance)
      return products.slice(0, 50)
    }
    
    const term = searchTerm.toLowerCase()
    const filtered = products.filter(product => 
      product.nameEn?.toLowerCase().includes(term) ||
      product.nameAr?.toLowerCase().includes(term) ||
      product.sku?.toLowerCase().includes(term) ||
      product.barcode?.toLowerCase().includes(term)
    )
    // Show up to 50 results for better visibility
    return filtered.slice(0, 50)
  }

  const addToCart = (product, rowIndex = null) => {
    // INSTANT UPDATE - Use requestAnimationFrame for immediate UI response
    requestAnimationFrame(() => {
      const qty = 1
      const rowTotal = qty * product.sellPrice
      const vatAmount = Math.round((rowTotal * 0.05) * 100) / 100
      const lineTotal = rowTotal + vatAmount
      
      // If rowIndex is provided, replace that specific row
      if (rowIndex !== null && rowIndex >= 0 && rowIndex < cart.length) {
        const newCart = [...cart]
        newCart[rowIndex] = {
          productId: product.id,
          productName: product.nameEn,
          sku: product.sku,
          unitType: product.unitType || 'CRTN', // Fallback to CRTN if null
          qty: qty,
          unitPrice: product.sellPrice,
          vatAmount: vatAmount,
          lineTotal: lineTotal
        }
        setCart(newCart)
        
        // Close dropdown IMMEDIATELY for this row
        setShowProductDropdown(prev => ({ ...prev, [rowIndex]: false }))
        setProductSearchTerms(prev => {
          const newTerms = { ...prev }
          delete newTerms[rowIndex]
          return newTerms
        })
      } else {
        // Otherwise, check if product already exists in cart
        const existingItemIndex = cart.findIndex(item => item.productId === product.id)
        
        if (existingItemIndex !== -1) {
          // Increment quantity of existing item
          setCart(cart.map((item, idx) => {
            if (idx === existingItemIndex) {
              const newQty = (typeof item.qty === 'number' ? item.qty : 0) + 1
              const rowTotal = newQty * item.unitPrice
              const vatAmount = Math.round((rowTotal * 0.05) * 100) / 100
              const lineTotal = rowTotal + vatAmount
              return { ...item, qty: newQty, vatAmount, lineTotal }
            }
            return item
          }))
        } else {
          // Add new item to cart
          setCart([...cart, {
            productId: product.id,
            productName: product.nameEn,
            sku: product.sku,
            unitType: product.unitType || 'CRTN', // Fallback to CRTN if null
            qty: qty,
            unitPrice: product.sellPrice,
            vatAmount: vatAmount,
            lineTotal: lineTotal
          }])
        }
        
        // Close all dropdowns
        setShowProductDropdown({})
      }
    })
  }

  const addEmptyRow = () => {
    setCart([...cart, {
      productId: null,
      productName: '',
      sku: '',
      unitType: '',
      qty: '',
      unitPrice: '',
      vatAmount: 0,
      lineTotal: 0
    }])
  }

  const updateCartItem = (index, field, value) => {
    const newCart = [...cart]
    
    // Handle empty string for number fields
    const numValue = value === '' ? '' : (field === 'qty' || field === 'unitPrice' ? Number(value) : value)
    newCart[index] = { ...newCart[index], [field]: numValue }
    
    // Calculate: Total = Qty × Price, VAT = Total × 5%, Amount = Total + VAT
    const qty = typeof newCart[index].qty === 'number' ? newCart[index].qty : 0
    const unitPrice = typeof newCart[index].unitPrice === 'number' ? newCart[index].unitPrice : 0
    
    if (unitPrice > 0 && qty > 0) {
      const rowTotal = qty * unitPrice
      const vatAmount = Math.round((rowTotal * 0.05) * 100) / 100
      const lineTotal = rowTotal + vatAmount
      
      newCart[index].vatAmount = vatAmount
      newCart[index].lineTotal = lineTotal
    } else {
      newCart[index].vatAmount = 0
      newCart[index].lineTotal = 0
    }
    
    setCart(newCart)
  }

  const removeFromCart = (index) => {
    setCart(cart.filter((_, i) => i !== index))
  }

  const calculateTotals = () => {
    const subtotal = cart.reduce((sum, item) => {
      const qty = typeof item.qty === 'number' ? item.qty : 0
      const unitPrice = typeof item.unitPrice === 'number' ? item.unitPrice : 0
      const rowTotal = qty * unitPrice
      return sum + rowTotal
    }, 0)
    
    const vatTotal = cart.reduce((sum, item) => sum + (item.vatAmount || 0), 0)
    const discountValue = typeof discount === 'number' ? discount : 0
    const grandTotal = subtotal + vatTotal - discountValue
    
    return { subtotal, vatTotal, grandTotal }
  }

  const handleDownloadPdf = async (saleId, invoiceNo) => {
    try {
      const response = await salesAPI.getInvoicePdf(saleId)
      const blob = response instanceof Blob ? response : new Blob([response], { type: 'application/pdf' })
      const url = window.URL.createObjectURL(blob)
      const a = document.createElement('a')
      a.href = url
      a.download = `${invoiceNo || 'invoice'}.pdf`
      document.body.appendChild(a)
      a.click()
      window.URL.revokeObjectURL(url)
      document.body.removeChild(a)
      toast.success('Invoice PDF downloaded')
    } catch (error) {
      console.error('Failed to download PDF:', error)
      toast.error('Failed to download PDF')
    }
  }

  const handlePrintReceipt = async () => {
    console.log('🖨️ Print Receipt Called')
    console.log('  - lastCreatedInvoice:', lastCreatedInvoice)
    
    if (!lastCreatedInvoice) {
      toast.error('No invoice to print. Please create an invoice first.')
      console.error('❌ lastCreatedInvoice is null or undefined')
      return
    }
    
    const saleId = lastCreatedInvoice.id
    const invoiceNo = lastCreatedInvoice.invoiceNo
    
    console.log(`  - Sale ID: ${saleId}, Invoice No: ${invoiceNo}`)
    
    if (!saleId) {
      toast.error('Invalid sale ID. Cannot print invoice.')
      console.error('❌ Sale ID is missing from lastCreatedInvoice')
      return
    }
    
    try {
      toast.loading('Preparing invoice for printing...', { id: 'print-toast' })
      
      // Get the PDF blob (same format as download)
      const pdfBlob = await salesAPI.getInvoicePdf(saleId)
      
      if (!pdfBlob) {
        throw new Error('No PDF data received from server')
      }
      
      // Ensure it's a proper Blob
      const blob = pdfBlob instanceof Blob ? pdfBlob : new Blob([pdfBlob], { type: 'application/pdf' })
      
      // Validate blob
      if (blob.size === 0) {
        throw new Error('PDF is empty - please check if invoice exists')
      }
      
      // Create object URL from blob (same format as PDF download)
      const pdfUrl = URL.createObjectURL(blob)
      
      // Open PDF in new window for printing
      const printWindow = window.open(pdfUrl, '_blank')
      
      if (!printWindow) {
        toast.error('Please allow pop-ups for this site to print')
        URL.revokeObjectURL(pdfUrl)
        return
      }
      
      // Function to trigger print
      const triggerPrint = () => {
        try {
          if (printWindow && !printWindow.closed) {
            printWindow.focus()
            printWindow.print()
            toast.dismiss('print-toast')
            toast.success('Print dialog opened')
            
            // Clean up URL after delay
            setTimeout(() => {
              URL.revokeObjectURL(pdfUrl)
            }, 5000)
            
            // Monitor if window is closed
            const checkInterval = setInterval(() => {
              if (printWindow.closed) {
                clearInterval(checkInterval)
                URL.revokeObjectURL(pdfUrl)
              }
            }, 500)
          } else {
            URL.revokeObjectURL(pdfUrl)
          }
        } catch (printErr) {
          console.error('Print trigger error:', printErr)
          toast('PDF opened. Please use Ctrl+P (Cmd+P on Mac) to print', {
            icon: 'ℹ️',
            duration: 5000
          })
          setTimeout(() => URL.revokeObjectURL(pdfUrl), 5000)
        }
      }
      
      // Wait for PDF to load (PDF viewers take time to render)
      // Try multiple times to ensure PDF is loaded
      let attempts = 0
      const maxAttempts = 15 // 3 seconds max wait
      
      const tryPrint = setInterval(() => {
        attempts++
        try {
          if (printWindow && !printWindow.closed) {
            // Check if we can access the window (indicates it's loaded)
            if (attempts >= 5) { // Wait at least 1 second
              clearInterval(tryPrint)
              triggerPrint()
            }
          } else {
            clearInterval(tryPrint)
            URL.revokeObjectURL(pdfUrl)
          }
        } catch (e) {
          // Cross-origin or other access errors - PDF is probably loaded
          clearInterval(tryPrint)
          triggerPrint()
        }
      }, 200)
      
      // Final fallback - try print after max wait
      setTimeout(() => {
        clearInterval(tryPrint)
        if (printWindow && !printWindow.closed) {
          triggerPrint()
        } else {
          URL.revokeObjectURL(pdfUrl)
        }
      }, 3000)
      
    } catch (error) {
      console.error('Print error:', error)
      console.error('Error details:', {
        message: error?.message,
        response: error?.response?.data,
        status: error?.response?.status
      })
      
      // Extract error message
      let errorMessage = 'Failed to prepare invoice for printing'
      
      if (error?.response?.status === 401) {
        errorMessage = 'Authentication required. Please login again.'
      } else if (error?.response?.status === 404) {
        errorMessage = 'Invoice not found. The invoice may have been deleted.'
      } else if (error?.response?.status >= 500) {
        errorMessage = 'Server error. Please try again later.'
      } else if (error?.response?.data?.message) {
        errorMessage = error.response.data.message
      } else if (error?.message) {
        errorMessage = error.message
      }
      
      toast.error(errorMessage)
      
      // Automatically try to download as fallback
      setTimeout(async () => {
        try {
          toast.loading('Attempting to download PDF as alternative...', { id: 'download-toast' })
          await handleDownloadPdf(saleId, invoiceNo)
          toast.dismiss('download-toast')
          toast.success('PDF downloaded. Open it and press Ctrl+P (Cmd+P on Mac) to print.')
        } catch (downloadErr) {
          console.error('Download fallback also failed:', downloadErr)
          toast.error('Both print and download failed. Please check the browser console for details.')
        }
      }, 2000)
    }
  }

  const handleWhatsAppShare = async () => {
    if (!lastCreatedInvoice) return
    
    try {
      const saleId = lastCreatedInvoice.id
      const invoiceNo = lastCreatedInvoice.invoiceNo || `INV-${saleId}`
      const customerName = selectedCustomer?.name || 'Cash Customer'
      const totals = calculateTotals()
      const date = new Date().toLocaleDateString()
      
      const message = `*Invoice ${invoiceNo}*\n\n` +
        `Customer: ${customerName}\n` +
        `Date: ${date}\n` +
        `Total: ${formatCurrency(totals.grandTotal)}\n\n` +
        `Please find the invoice attached.`
      
      const encodedMessage = encodeURIComponent(message)
      
      // Generate PDF blob first
      try {
        const pdfBlob = await salesAPI.getInvoicePdf(saleId)
        const blob = pdfBlob instanceof Blob ? pdfBlob : new Blob([pdfBlob], { type: 'application/pdf' })
        
        // Download PDF first so user can attach it
        const url = window.URL.createObjectURL(blob)
        const a = document.createElement('a')
        a.href = url
        a.style.display = 'none'
        a.download = `invoice_${invoiceNo}.pdf`
        document.body.appendChild(a)
        a.click()
        
        // Clean up download link
        setTimeout(() => {
          window.URL.revokeObjectURL(url)
          document.body.removeChild(a)
        }, 100)
        
        // Open WhatsApp Web with message
        const whatsappUrl = `https://wa.me/?text=${encodedMessage}`
        window.open(whatsappUrl, '_blank')
        
        toast.success('WhatsApp opened. Please attach the downloaded PDF')
      } catch (apiError) {
        console.error('API Error:', apiError)
        toast.error(apiError.message || 'Failed to generate PDF')
      }
    } catch (error) {
      console.error('WhatsApp share error:', error)
      toast.error(error.message || 'Failed to share via WhatsApp')
    }
  }

  const handleEmailShare = async () => {
    if (!lastCreatedInvoice) return
    
    try {
      const saleId = lastCreatedInvoice.id
      const invoiceNo = lastCreatedInvoice.invoiceNo || `INV-${saleId}`
      
      // Get customer email or prompt
      let customerEmail = selectedCustomer?.email
      
      if (!customerEmail) {
        customerEmail = prompt('Enter customer email address:')
        if (!customerEmail) {
          toast.error('Email address required')
          return
        }
      }
      
      // Send email via API
      try {
        const response = await salesAPI.sendInvoiceEmail(saleId, customerEmail)
        if (response.success) {
          toast.success(`Invoice sent to ${customerEmail}`)
        } else {
          toast.error(response.message || 'Failed to send email')
        }
      } catch (emailError) {
        console.error('Email send error:', emailError)
        // Fallback: Create mailto link with PDF attachment suggestion
        const subject = encodeURIComponent(`Invoice ${invoiceNo}`)
        const body = encodeURIComponent(`Please find invoice ${invoiceNo} attached.\n\nThank you for your business!`)
        window.location.href = `mailto:${customerEmail}?subject=${subject}&body=${body}`
        toast('Email client opened. Please attach the PDF manually if needed.', {
          icon: 'ℹ️',
          duration: 5000
        })
      }
    } catch (error) {
      console.error('Email share error:', error)
      toast.error(error.message || 'Failed to send email')
    }
  }

  const handleCloseInvoiceOptions = async () => {
    setShowInvoiceOptionsModal(false)
    setLastCreatedInvoice(null)
    // Refresh all data after billing
    await Promise.all([
      loadProducts(),
      loadCustomers()
    ])
    // Clear cart and reset for new invoice
    handleNewInvoice()
  }

  const handleSave = async () => {
    // Prevent multiple clicks
    if (loading || loadingSale) {
      toast.error('Please wait, operation in progress...')
      return
    }

    if (cart.length === 0) {
      toast.error('Cart is empty')
      return
    }

    // Filter out empty rows
    const validCart = cart.filter(item => item.productId && item.qty > 0 && item.unitPrice > 0)
    if (validCart.length === 0) {
      toast.error('Please add at least one valid product')
      return
    }

    // Validate quantities and prices
    for (const item of validCart) {
      if (item.qty <= 0 || item.qty > 100000) {
        toast.error(`Invalid quantity for ${item.productName || 'product'}. Must be between 1 and 100,000.`)
        return
      }
      if (item.unitPrice <= 0 || item.unitPrice > 1000000) {
        toast.error(`Invalid price for ${item.productName || 'product'}. Must be between 0.01 and 1,000,000.`)
        return
      }
    }

    setLoading(true)
    try {
      const totals = calculateTotals()
      
      // Validate items before creating sale data
      if (!validCart || validCart.length === 0) {
        toast.error('Please add at least one product to the invoice')
        setLoading(false)
        return
      }
      
      const saleData = {
        customerId: selectedCustomer?.id || null,
        items: validCart.map(item => ({
          productId: item.productId,
          unitType: item.unitType || 'CRTN', // Default unit type
          qty: Number(item.qty) || 0,
          unitPrice: Number(item.unitPrice) || 0
        })).filter(item => item.productId && item.qty > 0 && item.unitPrice > 0), // Filter out invalid items
        discount: discount || 0,
        // Only include payment if method is not "Pending" and amount is provided or should use full amount
        payments: (paymentMethod !== 'Pending') ? [{
          method: paymentMethod,
          amount: paymentAmount ? parseFloat(paymentAmount) : totals.grandTotal // Use grandTotal if amount not specified
        }] : [],
        notes: notes || null,
        // Add EditReason - required for Staff, optional for Admin
        editReason: isEditMode ? editReason : undefined,
        // Add invoice date - send as YYYY-MM-DD string at noon UTC to avoid timezone issues
        invoiceDate: invoiceDate ? `${invoiceDate}T12:00:00.000Z` : undefined
      }
      
      // Final validation
      if (!saleData.items || saleData.items.length === 0) {
        toast.error('Please add at least one valid product to the invoice')
        setLoading(false)
        return
      }

      // Only admins can edit invoices
      if (isEditMode && user?.role?.toLowerCase() !== 'admin') {
        toast.error('Only Administrators can edit invoices')
        setLoading(false)
        return
      }

      let response
      if (isEditMode && editingSaleId) {
        // Update existing sale - include RowVersion for concurrency control
        const updateData = {
          customerId: saleData.customerId,
          items: saleData.items,
          discount: saleData.discount,
          payments: saleData.payments || [],
          notes: saleData.notes || null,
          ...(saleData.editReason && { editReason: saleData.editReason }),
          ...(editingSale?.rowVersion && { rowVersion: editingSale.rowVersion }),
          ...(saleData.invoiceDate && { invoiceDate: saleData.invoiceDate })
        }
        
        // Log the update request for debugging
        console.log('Updating invoice:', {
          saleId: editingSaleId,
          updateData,
          hasRowVersion: !!editingSale?.rowVersion,
          itemsCount: updateData.items?.length
        })
        
        response = await salesAPI.updateSale(editingSaleId, updateData)
        if (response.success) {
          const invoiceNo = response.data?.invoiceNo
          const saleId = response.data?.id
          toast.success(`Invoice ${invoiceNo || editingSaleId} updated successfully!`)
          
          // Refresh products and customers after update (non-blocking for better UX)
          Promise.all([
            loadProducts(),
            loadCustomers(),
            loadNextInvoiceNumber()
          ]).catch(err => console.error('Error refreshing data:', err))
          
          // Clear edit mode and URL param
          setIsEditMode(false)
          setEditingSaleId(null)
          setEditingSale(null)
          setEditReason('')
          setSearchParams({})
          
          // Store invoice data and show options modal
          if (saleId) {
            setLastCreatedInvoice({
              id: saleId,
              invoiceNo: invoiceNo,
              data: response.data
            })
            setShowInvoiceOptionsModal(true)
            
            // If we came from customer ledger, offer to go back
            const cameFromLedger = document.referrer.includes('/ledger')
            if (cameFromLedger) {
              // Small delay to show success message first
              setTimeout(() => {
                if (window.confirm('Invoice updated successfully! Would you like to return to Customer Ledger?')) {
                  navigate('/ledger')
                }
              }, 1000)
            }
          } else {
            // Clear cart and reset for new invoice
            handleNewInvoice()
          }
        } else {
          const errorMsg = response.message || response.errors?.[0] || 'Failed to update invoice'
          toast.error(errorMsg)
        }
      } else {
        // Create new sale
        console.log('📤 Sending Create Sale Request:')
        console.log('  - Full saleData:', JSON.stringify(saleData, null, 2))
        console.log('  - Items count:', saleData.items?.length)
        console.log('  - Items detail:', saleData.items)
        console.log('  - Customer ID:', saleData.customerId)
        console.log('  - Grand Total:', totals.grandTotal)
        console.log('  - Discount:', saleData.discount)
        console.log('  - Payments:', saleData.payments)
        
        response = await salesAPI.createSale(saleData)
        
        console.log('📦 Create Sale Response:', response)
        
        if (response.success) {
          const invoiceNo = response.data?.invoiceNo
          const saleId = response.data?.id
          
          if (!saleId) {
            console.error('⚠️ Sale created but no ID returned:', response.data)
            toast.error('Invoice created but ID missing. Please refresh and check Sales list.')
            setLoading(false)
            return
          }
          
          toast.success(`Invoice ${invoiceNo || 'saved'} successfully!`)
          
          // Refresh products and customers after billing (non-blocking for better UX)
          Promise.all([
            loadProducts(),
            loadCustomers(),
            loadNextInvoiceNumber() // Refresh invoice number for next invoice
          ]).catch(err => console.error('Error refreshing data:', err))
          
          // Store invoice data and show options modal
          if (saleId) {
            setLastCreatedInvoice({
              id: saleId,
              invoiceNo: invoiceNo,
              data: response.data
            })
            setShowInvoiceOptionsModal(true)
          } else {
            // Clear cart and reset for new invoice if no saleId
            handleNewInvoice()
          }
        } else {
          toast.error(response.message || 'Failed to save sale')
        }
      }
    } catch (error) {
      console.error('Error saving/updating invoice:', error)
      console.error('Error details:', {
        message: error?.message,
        response: error?.response?.data,
        status: error?.response?.status,
        statusText: error?.response?.statusText,
        isEditMode,
        url: error?.config?.url,
        method: error?.config?.method
      })
      
      if (isEditMode) {
        // Update-specific error handling
        let errorMsg = 'Failed to update invoice. Please try again.'
        
        if (error?.response) {
          // Server responded with error
          const responseData = error.response.data
          if (responseData?.message) {
            errorMsg = responseData.message
          } else if (responseData?.errors && Array.isArray(responseData.errors) && responseData.errors.length > 0) {
            errorMsg = responseData.errors[0]
          } else if (responseData?.error) {
            errorMsg = responseData.error
          } else if (error.response.status === 500) {
            errorMsg = 'Server error occurred. Please check backend logs for details.'
          } else if (error.response.status === 401) {
            errorMsg = 'Unauthorized. Please log in again.'
          } else if (error.response.status === 403) {
            errorMsg = 'You do not have permission to update invoices.'
          }
        } else if (error?.message) {
          // Network or other error
          errorMsg = error.message
        }
        
        toast.error(errorMsg, { duration: 6000 })
      } else {
        // Create-specific error handling
        let errorMsg = 'Failed to save sale'
        
        if (error.response?.status === 400) {
          // Extract detailed error message from response
          const responseData = error.response.data
          console.log('❌ 400 Bad Request - Full Response:', responseData)
          
          if (responseData?.message) {
            errorMsg = responseData.message
          } else if (responseData?.errors) {
            // Handle both array and object formats
            if (Array.isArray(responseData.errors)) {
              errorMsg = responseData.errors.join('\n')
            } else if (typeof responseData.errors === 'object') {
              // ASP.NET validation errors format: { "field": ["error1", "error2"] }
              const errorMessages = Object.entries(responseData.errors)
                .map(([field, msgs]) => `${field}: ${Array.isArray(msgs) ? msgs.join(', ') : msgs}`)
                .join('\n')
              errorMsg = errorMessages || 'Validation failed'
            }
          } else if (responseData?.title) {
            // ASP.NET problem details format
            errorMsg = responseData.title
          } else {
            errorMsg = 'Bad request - please check product data, stock, and quantities'
          }
        } else if (error.response?.status === 500) {
          errorMsg = 'Server error - please check backend console for details'
        } else if (error.message) {
          errorMsg = error.message
        }
        
        toast.error(errorMsg, { duration: 8000 })
        
        // Log detailed error for debugging
        console.log('📋 Error occurred during save')
        console.log('❌ Backend Error Response:', error.response?.data)
      }
    } finally {
      setLoading(false)
    }
  }

  const handleNewInvoice = () => {
    setCart([])
    setSelectedCustomer(null)
    setPaymentMethod('Pending') // Default to credit invoice
    setPaymentAmount('')
    setNotes('')
    setDiscount(0)
    setDiscountInput('')
    setProductSearchTerms({}) // Clear all search terms
    setIsEditMode(false)
    setEditingSaleId(null)
    setEditingSale(null)
    setEditReason('')
    setSearchParams({}) // Clear URL params
    setShowProductDropdown({}) // Close all dropdowns
    // Reset invoice date to today
    const today = new Date()
    setInvoiceDate(today.toISOString().split('T')[0])
  }

  const totals = calculateTotals()

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-slate-50 flex flex-col max-w-full overflow-x-hidden">
      {/* TAX INVOICE Header - Premium Blue - Responsive */}
      <div className="bg-gradient-to-r from-blue-600 to-blue-700 text-white px-2 sm:px-4 lg:px-6 py-2 sm:py-3 lg:py-4 flex-shrink-0 shadow-lg">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2 sm:gap-0">
          <div className="min-w-0 flex-1">
            <h1 className="text-lg sm:text-xl md:text-2xl lg:text-3xl font-bold mb-0.5 sm:mb-1 drop-shadow-md truncate">TAX INVOICE</h1>
            <p className="text-xs sm:text-sm text-blue-100">فاتورة ضريبية</p>
          </div>
          <div className="flex items-center gap-1.5 sm:gap-2 lg:space-x-4 w-full sm:w-auto">
            <button
              onClick={() => setShowCustomerSearch(true)}
              className="flex-1 sm:flex-none px-2 sm:px-3 lg:px-4 py-1.5 sm:py-2 text-xs sm:text-sm font-medium bg-white text-blue-700 border-2 border-white rounded-lg hover:bg-blue-50 hover:border-blue-300 transition-colors flex items-center justify-center shadow-md min-w-0"
            >
              <User className="h-3 w-3 sm:h-3.5 sm:w-3.5 lg:h-4 lg:w-4 mr-1 sm:mr-1.5 lg:mr-2 flex-shrink-0" />
              <span className="truncate text-xs sm:text-sm">{selectedCustomer ? selectedCustomer.name : 'Select Customer'}</span>
            </button>
            <button
              onClick={handleNewInvoice}
              className="px-2 sm:px-3 lg:px-4 py-1.5 sm:py-2 text-xs sm:text-sm font-medium bg-blue-800 text-white border-2 border-blue-900 rounded-lg hover:bg-blue-900 transition-colors shadow-md flex-shrink-0"
            >
              New Invoice
            </button>
          </div>
        </div>
      </div>

      {/* Edit Mode Indicator */}
      {isEditMode && (
        <div className="bg-yellow-500 text-white px-3 sm:px-6 py-2 flex items-center justify-center gap-2 shadow-md">
          <AlertTriangle className="h-4 w-4 sm:h-5 sm:w-5" />
          <span className="text-sm sm:text-base font-semibold">
            EDIT MODE: Updating Invoice #{editingSaleId} - Changes will update stock and customer balance
          </span>
        </div>
      )}

      {/* Loading Sale Indicator */}
      {loadingSale && (
        <div className="bg-blue-500 text-white px-3 sm:px-6 py-2 flex items-center justify-center gap-2 shadow-md">
          <div className="animate-spin rounded-full h-4 w-4 border-b-2 border-white"></div>
          <span className="text-sm sm:text-base font-semibold">Loading invoice for editing...</span>
        </div>
      )}

      {/* Customer Info Section - Always Show - Light Blue - Responsive */}
      <div className="bg-gradient-to-r from-blue-50 to-sky-50 border-b-2 border-blue-300 px-2 sm:px-4 lg:px-6 py-2 sm:py-3 lg:py-4 flex-shrink-0 shadow-sm">
        <div className="grid grid-cols-2 sm:grid-cols-3 lg:grid-cols-5 gap-1.5 sm:gap-2 lg:gap-4 text-xs sm:text-sm overflow-x-auto">
          <div className="bg-white rounded-lg px-2 sm:px-3 py-1.5 sm:py-2 shadow-sm border border-blue-200">
            <span className="font-medium text-blue-700">Invoice No:</span>
            <span className={`ml-1 sm:ml-2 font-semibold font-mono text-xs sm:text-sm ${
              isEditMode ? 'text-blue-700' : 'text-gray-900'
            }`}>
              {isEditMode && editingSale ? editingSale.invoiceNo : nextInvoiceNumber}
            </span>
            {isEditMode && <span className="ml-2 text-xs text-blue-600">(Read-only)</span>}
          </div>
          <div className="bg-white rounded-lg px-3 py-2 shadow-sm border border-blue-200">
            <span className="font-medium text-blue-700">Customer:</span>
            <span className="ml-2 text-gray-900 font-semibold">
              {selectedCustomer ? selectedCustomer.name : 'Cash Customer'}
            </span>
          </div>
          <div className="bg-white rounded-lg px-3 py-2 shadow-sm border border-blue-200">
            <span className="font-medium text-blue-700">Address:</span>
            <span className="ml-2 text-gray-900">
              {selectedCustomer?.address || '-'}
            </span>
          </div>
          <div className="bg-white rounded-lg px-3 py-2 shadow-sm border border-blue-200">
            <span className="font-medium text-blue-700">TRN:</span>
            <span className="ml-2 text-gray-900 font-mono">
              {selectedCustomer?.trn || '-'}
            </span>
          </div>
          <div className="bg-white rounded-lg px-3 py-2 shadow-sm border border-blue-200">
            <span className="font-medium text-blue-700">Pending Balance:</span>
            <span className={`ml-2 font-bold ${selectedCustomer?.balance < 0 ? 'text-green-600' : selectedCustomer?.balance > 0 ? 'text-red-600' : 'text-gray-600'}`}>
              {formatBalance(selectedCustomer?.balance || 0)}
            </span>
          </div>
        </div>
      </div>

      {/* Main Content */}
      <div className="flex-1 flex flex-col overflow-hidden w-full">
        {/* Invoice Info Row */}
        <div className="bg-white border-b-2 border-gray-300 px-6 py-3 flex-shrink-0">
          <div className="grid grid-cols-2 gap-4 text-sm">
            <div>
              <span className="font-medium text-gray-600">Invoice No:</span>
              <span className="ml-2 text-gray-900 font-mono">(Auto-generated on save)</span>
            </div>
            <div className="text-right flex items-center justify-end gap-3">
              <label className="font-medium text-gray-600">Invoice Date:</label>
              <input
                type="date"
                value={invoiceDate}
                onChange={(e) => setInvoiceDate(e.target.value)}
                className="px-3 py-1.5 border-2 border-blue-300 rounded-lg text-gray-900 font-semibold focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white"
              />
              {isEditMode && (
                <span className="text-xs text-blue-600">(Editable)</span>
              )}
            </div>
          </div>
        </div>

        {/* Items Table - FULL WIDTH TALLY STYLE (Desktop) / CARD LAYOUT (Mobile) */}
        <div className="flex-1 overflow-hidden bg-gray-50" style={{ display: 'flex', flexDirection: 'column' }}>
          <div className="flex-1 overflow-y-auto px-2 py-2 md:pb-2"> {/* Removed fixed bottom padding for mobile - now fully scrollable */}
            {/* Desktop Table View - with horizontal scroll on small screens */}
            <div className="hidden md:block bg-white rounded-lg border-2 border-gray-300 shadow-lg overflow-x-auto">
              <div>
                <table className="w-full text-xs sm:text-sm border-collapse" style={{ tableLayout: 'auto' }}>
                <thead className="bg-gray-100 border-2 border-gray-300">
                  <tr>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-left font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-12 text-xs sm:text-sm">SL<br/><span className="text-xs sm:text-xs font-normal text-gray-600">رقم</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-left font-bold text-gray-900 border-r-2 border-gray-300 w-80 text-xs sm:text-sm">Description<br/><span className="text-xs sm:text-xs font-normal text-gray-600">التفاصيل</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-center font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-28 text-xs sm:text-sm">Unit<br/><span className="text-xs sm:text-xs font-normal text-gray-600">الوحدة</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-center font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-24 text-xs sm:text-sm">Qty<br/><span className="text-xs sm:text-xs font-normal text-gray-600">الكمية</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-right font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-32 text-xs sm:text-sm">Unit Price<br/><span className="text-xs sm:text-xs font-normal text-gray-600">سعر الوحدة</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-right font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-28 text-xs sm:text-sm">Total<br/><span className="text-xs sm:text-xs font-normal text-gray-600">الإجمالي</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-right font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-28 text-xs sm:text-sm">Vat:5%<br/><span className="text-xs sm:text-xs font-normal text-gray-600">ضريبة 5%</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-right font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-32 text-xs sm:text-sm">Amount<br/><span className="text-xs sm:text-xs font-normal text-gray-600">المبلغ</span></th>
                    <th className="px-2 sm:px-3 py-2 sm:py-3 text-center font-bold text-gray-900 border-r-2 border-gray-300 whitespace-nowrap w-24 text-xs sm:text-sm">Actions<br/><span className="text-xs sm:text-xs font-normal text-gray-600">إجراءات</span></th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-gray-200">
                  {cart.length === 0 ? (
                    <tr>
                      <td colSpan="9" className="px-4 py-12 text-center text-gray-500 text-base">
                        No items in cart. Click + to add products
                      </td>
                    </tr>
                  ) : (
                    cart.map((item, index) => (
                      <tr key={index} className="hover:bg-gray-50 border-b border-gray-200">
                        <td className="px-2 sm:px-3 py-3 sm:py-4 text-center border-r-2 border-gray-200 font-medium text-sm align-middle">{index + 1}</td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 border-r-2 border-gray-200 min-h-[80px] align-top" style={{ position: 'relative', overflow: 'visible' }}>
                          <div className="relative product-dropdown-container" style={{ zIndex: showProductDropdown[index] ? 9999 : 1 }}>
                            {item.productId ? (
                              <div className="py-2">
                                <p className="font-semibold text-gray-900 text-base leading-snug break-words">{item.productName}</p>
                                <p className="text-xs text-gray-500 mt-1.5">{item.sku}</p>
                              </div>
                            ) : (
                              <div className="relative product-dropdown-container">
                                <input
                                  type="text"
                                  ref={(el) => productSearchRefs.current[index] = el}
                                  value={productSearchTerms[index] || ''}
                                  onChange={(e) => {
                                    const searchValue = e.target.value
                                    setProductSearchTerms(prev => ({ ...prev, [index]: searchValue }))
                                    // Auto-open dropdown when user starts typing
                                    if (searchValue.trim() && !showProductDropdown[index]) {
                                      setShowProductDropdown(prev => ({ ...prev, [index]: true }))
                                    }
                                  }}
                                  onFocus={() => {
                                    // Always show dropdown when focused
                                      setShowProductDropdown(prev => ({ ...prev, [index]: true }))
                                    // If no search term, show all products
                                    if (!productSearchTerms[index] || !productSearchTerms[index].trim()) {
                                      // Keep dropdown open to show all products
                                    }
                                  }}
                                  onClick={(e) => e.stopPropagation()}
                                  placeholder="Type to search product..."
                                  className="w-full px-3 py-3 border-2 border-blue-300 rounded-lg text-base focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500 bg-white min-h-[52px] font-medium"
                                />
                                {showProductDropdown[index] && (
                                  <>
                                    {/* Arrow pointing down */}
                                    <div className="absolute z-[9999] top-full left-4 w-0 h-0 border-l-[10px] border-r-[10px] border-t-[10px] border-l-transparent border-r-transparent border-t-blue-400" style={{ marginTop: '-1px' }}></div>
                                    {/* Dropdown - Positioned ABOVE table overflow with HIGHEST z-index */}
                                    <div 
                                      className="fixed bg-white border-2 border-blue-400 rounded-lg shadow-2xl z-[10000]"
                                      style={{ 
                                        maxHeight: '500px',
                                        width: '600px',
                                        overflowY: 'auto',
                                        overflowX: 'hidden',
                                        top: `${productSearchRefs.current[index]?.getBoundingClientRect().bottom + 2}px`,
                                        left: `${productSearchRefs.current[index]?.getBoundingClientRect().left}px`,
                                        boxShadow: '0 10px 25px rgba(0,0,0,0.3)'
                                      }}
                                      onMouseDown={(e) => e.stopPropagation()}
                                      onClick={(e) => e.stopPropagation()}
                                    >
                                      {(() => {
                                        const filtered = getFilteredProducts(index)
                                        const searchTerm = productSearchTerms[index] || ''
                                        const totalProducts = searchTerm.trim() 
                                          ? products.filter(p => 
                                              p.nameEn?.toLowerCase().includes(searchTerm.toLowerCase()) ||
                                              p.nameAr?.toLowerCase().includes(searchTerm.toLowerCase()) ||
                                              p.sku?.toLowerCase().includes(searchTerm.toLowerCase()) ||
                                              p.barcode?.toLowerCase().includes(searchTerm.toLowerCase())
                                            ).length
                                          : products.length
                                        const showingCount = filtered.length
                                        const hasMore = totalProducts > showingCount

                                        return filtered.length > 0 ? (
                                        <>
                                          {/* Product list */}
                                            {filtered.map((product) => (
                                            <div
                                              key={product.id}
                                                className="p-2.5 border-b border-gray-200 hover:bg-blue-50 cursor-pointer transition-colors active:bg-blue-100"
                                              onMouseDown={(e) => {
                                                e.preventDefault()
                                                e.stopPropagation()
                                              }}
                                              onClick={(e) => {
                                                e.preventDefault()
                                                e.stopPropagation()
                                                addToCart(product, index)
                                              }}
                                            >
                                              <div className="flex items-center justify-between w-full">
                                                  <div className="flex-1 min-w-0">
                                                    <p className="font-medium text-sm text-gray-900 truncate">{product.nameEn}</p>
                                                  <p className="text-xs text-gray-600">AED {product.sellPrice.toFixed(2)}</p>
                                                </div>
                                                  <div className="text-right ml-2 flex-shrink-0">
                                                  <p className={`text-xs font-semibold ${product.stockQty <= (product.reorderLevel || 0) ? 'text-red-600' : 'text-green-600'}`}>
                                                    Stock: {product.stockQty} {product.unitType || 'KG'}
                                                  </p>
                                                  {product.stockQty <= (product.reorderLevel || 0) && (
                                                    <p className="text-xs text-red-500">Low Stock!</p>
                                                  )}
                                                </div>
                                              </div>
                                            </div>
                                          ))}
                                            {/* Show more indicator */}
                                            {hasMore && (
                                              <div className="p-2 bg-blue-50 border-t border-blue-200 text-center">
                                                <p className="text-xs text-blue-700 font-medium">
                                                  Showing {showingCount} of {totalProducts} products. Type to search for more...
                                                </p>
                                              </div>
                                            )}
                                        </>
                                      ) : (
                                        <div className="p-3 text-center">
                                          <p className="text-sm text-gray-500">No products found</p>
                                            <p className="text-xs text-gray-400 mt-1">Try a different search term</p>
                                        </div>
                                        )
                                      })()}
                                    </div>
                                  </>
                                )}
                              </div>
                            )}
                          </div>
                        </td>
                        {/* Unit Column: Editable numeric quantity (1.5, 1, 0.5) */}
                        <td className="px-2 sm:px-3 py-3 sm:py-4 border-r-2 border-gray-200 align-middle">
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            className="w-full px-3 py-3 border-2 border-gray-300 rounded-lg text-center focus:outline-none focus:ring-2 focus:ring-blue-500 text-base font-semibold min-h-[52px]"
                            value={item.qty === '' ? '' : item.qty}
                            onChange={(e) => updateCartItem(index, 'qty', e.target.value)}
                            placeholder="1.5"
                          />
                        </td>
                        {/* Qty Column: Editable unit type dropdown (CRTN, KG, PIECE, etc.) */}
                        <td className="px-2 sm:px-3 py-3 sm:py-4 border-r-2 border-gray-200 align-middle">
                          <select
                            className="w-full px-3 py-3 border-2 border-gray-300 rounded-lg text-center focus:outline-none focus:ring-2 focus:ring-blue-500 text-base font-medium uppercase min-h-[52px]"
                            value={item.unitType || 'CRTN'}
                            onChange={(e) => updateCartItem(index, 'unitType', e.target.value)}
                          >
                            <option value="CRTN">CRTN</option>
                            <option value="KG">KG</option>
                            <option value="PIECE">PIECE</option>
                            <option value="BOX">BOX</option>
                            <option value="PKG">PKG</option>
                            <option value="BAG">BAG</option>
                            <option value="PC">PC</option>
                            <option value="UNIT">UNIT</option>
                          </select>
                        </td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 border-r-2 border-gray-200 align-middle">
                          <input
                            type="number"
                            min="0"
                            step="0.01"
                            className="w-full px-3 py-3 border-2 border-gray-300 rounded-lg text-right focus:outline-none focus:ring-2 focus:ring-blue-500 text-base font-semibold min-h-[52px]"
                            value={item.unitPrice === '' ? '' : item.unitPrice}
                            onChange={(e) => updateCartItem(index, 'unitPrice', e.target.value)}
                          />
                        </td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 text-right border-r-2 border-gray-200 font-semibold text-base align-middle">
                          {(() => {
                            const qty = typeof item.qty === 'number' ? item.qty : 0
                            const price = typeof item.unitPrice === 'number' ? item.unitPrice : 0
                            return (qty * price).toFixed(2)
                          })()}
                        </td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 text-right border-r-2 border-gray-200 font-semibold text-base align-middle">
                          {item.vatAmount.toFixed(2)}
                        </td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 text-right font-bold border-r-2 border-gray-200 text-base align-middle">
                          {item.lineTotal.toFixed(2)}
                        </td>
                        <td className="px-2 sm:px-3 py-3 sm:py-4 text-center align-middle border-r-2 border-gray-200">
                          <button
                            onClick={() => removeFromCart(index)}
                            className="text-red-600 hover:text-red-800 hover:bg-red-50 p-2 rounded-lg transition-colors inline-flex items-center justify-center"
                            title="Delete item"
                          >
                            <Trash2 className="h-5 w-5" />
                          </button>
                        </td>
                      </tr>
                    ))
                  )}
                </tbody>
              </table>
            </div>
          </div>
          
          {/* Mobile Card Layout - COMPLETELY REDESIGNED FOR EASY BILLING - fully scrollable */}
          <div className="md:hidden space-y-2">
            {/* Add Product Button - Always Visible on Top */}
            <button
              onClick={addEmptyRow}
              className="w-full px-4 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 flex items-center justify-center text-sm font-bold shadow-lg"
            >
              <Plus className="h-5 w-5 mr-2" />
              Add Product to Bill
            </button>
            
            {cart.length === 0 ? (
              <div className="bg-white rounded-lg border-2 border-dashed border-gray-300 p-8 text-center">
                <div className="text-gray-400 mb-2">
                  <Calculator className="h-12 w-12 mx-auto" />
                </div>
                <p className="text-gray-600 font-medium">No items in cart</p>
                <p className="text-gray-400 text-sm mt-1">Click 'Add Product' button above</p>
              </div>
            ) : (
              cart.map((item, index) => (
                <div key={index} className="bg-white rounded-lg border-2 border-gray-300 shadow-sm">
                  {/* Header: Product Name or Search */}
                  <div className="bg-gradient-to-r from-blue-50 to-blue-100 p-3 border-b-2 border-blue-200 flex items-center justify-between">
                    <div className="flex-1">
                      {item.productId ? (
                        <div>
                          <p className="font-bold text-gray-900 text-sm">#{index + 1} {item.productName}</p>
                          <p className="text-xs text-gray-600">{item.sku}</p>
                        </div>
                      ) : (
                        <div className="relative">
                          <p className="text-xs text-gray-600 mb-1">#{index + 1} Select Product:</p>
                          <input
                            type="text"
                            ref={(el) => productSearchRefs.current[index] = el}
                            value={productSearchTerms[index] || ''}
                            onChange={(e) => {
                              const searchValue = e.target.value
                              setProductSearchTerms(prev => ({ ...prev, [index]: searchValue }))
                              if (searchValue.trim() && !showProductDropdown[index]) {
                                setShowProductDropdown(prev => ({ ...prev, [index]: true }))
                              }
                            }}
                            onFocus={() => setShowProductDropdown(prev => ({ ...prev, [index]: true }))}
                            onClick={(e) => e.stopPropagation()}
                            placeholder="🔍 Search product name or code..."
                            className="w-full px-3 py-2.5 border-2 border-blue-400 rounded-lg text-sm font-medium focus:outline-none focus:ring-2 focus:ring-blue-500 bg-white"
                          />
                          {showProductDropdown[index] && (
                            <div 
                              className="fixed bg-white border-2 border-blue-400 rounded-lg shadow-2xl z-[9998]"
                              style={{ 
                                maxHeight: '60vh',
                                width: 'calc(100vw - 32px)',
                                top: `${productSearchRefs.current[index]?.getBoundingClientRect().bottom + 4}px`,
                                left: '16px'
                              }}
                              onMouseDown={(e) => e.stopPropagation()}
                              onClick={(e) => e.stopPropagation()}
                            >
                              {(() => {
                                const filtered = getFilteredProducts(index)
                                return filtered.length > 0 ? (
                                  <div className="divide-y divide-gray-200">
                                    {filtered.map((product) => (
                                      <div
                                        key={product.id}
                                        className="p-3 hover:bg-blue-50 active:bg-blue-100 cursor-pointer"
                                        onMouseDown={(e) => {
                                          e.preventDefault()
                                          e.stopPropagation()
                                        }}
                                        onClick={(e) => {
                                          e.preventDefault()
                                          e.stopPropagation()
                                          addToCart(product, index)
                                        }}
                                      >
                                        <div className="flex items-start justify-between">
                                          <div className="flex-1 min-w-0">
                                            <p className="font-semibold text-sm text-gray-900 truncate">{product.nameEn}</p>
                                            <p className="text-xs text-gray-600 mt-0.5">AED {product.sellPrice.toFixed(2)}</p>
                                          </div>
                                          <span className={`ml-2 text-xs font-medium px-2 py-0.5 rounded ${
                                            product.stockQty > (product.reorderLevel || 0) ? 'bg-green-100 text-green-800' : 'bg-red-100 text-red-800'
                                          }`}>
                                            Stock: {product.stockQty}
                                          </span>
                                        </div>
                                      </div>
                                    ))}
                                  </div>
                                ) : (
                                  <div className="p-4 text-center text-gray-500 text-sm">
                                    No products found
                                  </div>
                                )
                              })()}
                            </div>
                          )}
                        </div>
                      )}
                    </div>
                    <button
                      onClick={() => removeFromCart(index)}
                      className="ml-2 text-red-600 hover:text-red-800 p-2 rounded-lg hover:bg-red-50"
                    >
                      <Trash2 className="h-5 w-5" />
                    </button>
                  </div>
                  
                  {/* Body: Input Fields - Large Touch Targets */}
                  <div className="p-3 space-y-2">
                    {/* Row 1: Quantity and Unit */}
                    <div className="grid grid-cols-2 gap-2">
                      <div>
                        <label className="block text-xs font-bold text-gray-700 mb-1">Quantity</label>
                        <input
                          type="number"
                          min="0"
                          step="0.01"
                          className="w-full px-3 py-2.5 border-2 border-gray-300 rounded-lg text-center text-base font-bold focus:outline-none focus:ring-2 focus:ring-blue-500"
                          value={item.qty === '' ? '' : item.qty}
                          onChange={(e) => updateCartItem(index, 'qty', e.target.value)}
                          placeholder="1"
                        />
                      </div>
                      <div>
                        <label className="block text-xs font-bold text-gray-700 mb-1">Unit Type</label>
                        <select
                          className="w-full px-2 py-2.5 border-2 border-gray-300 rounded-lg text-center text-sm font-bold uppercase focus:outline-none focus:ring-2 focus:ring-blue-500"
                          value={item.unitType || 'CRTN'}
                          onChange={(e) => updateCartItem(index, 'unitType', e.target.value)}
                        >
                          <option value="CRTN">CRTN</option>
                          <option value="KG">KG</option>
                          <option value="PIECE">PIECE</option>
                          <option value="BOX">BOX</option>
                          <option value="PKG">PKG</option>
                          <option value="BAG">BAG</option>
                          <option value="PC">PC</option>
                          <option value="UNIT">UNIT</option>
                        </select>
                      </div>
                    </div>
                    
                    {/* Row 2: Unit Price */}
                    <div>
                      <label className="block text-xs font-bold text-gray-700 mb-1">Unit Price (AED)</label>
                      <input
                        type="number"
                        min="0"
                        step="0.01"
                        className="w-full px-3 py-2.5 border-2 border-gray-300 rounded-lg text-right text-base font-bold focus:outline-none focus:ring-2 focus:ring-blue-500"
                        value={item.unitPrice === '' ? '' : item.unitPrice}
                        onChange={(e) => updateCartItem(index, 'unitPrice', e.target.value)}
                        placeholder="0.00"
                      />
                    </div>
                    
                    {/* Row 3: Calculated Values - Read Only */}
                    <div className="bg-gray-50 rounded-lg p-2 border border-gray-300">
                      <div className="grid grid-cols-3 gap-2 text-xs">
                        <div className="text-center">
                          <p className="text-gray-600 font-medium">Total</p>
                          <p className="font-bold text-gray-900">{(() => {
                            const qty = typeof item.qty === 'number' ? item.qty : 0
                            const price = typeof item.unitPrice === 'number' ? item.unitPrice : 0
                            return (qty * price).toFixed(2)
                          })()}</p>
                        </div>
                        <div className="text-center">
                          <p className="text-gray-600 font-medium">VAT 5%</p>
                          <p className="font-bold text-blue-600">{item.vatAmount.toFixed(2)}</p>
                        </div>
                        <div className="text-center">
                          <p className="text-gray-600 font-medium">Amount</p>
                          <p className="font-bold text-green-700 text-sm">{item.lineTotal.toFixed(2)}</p>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              ))
            )}
          </div>
          </div>

          {/* Add Row Button - Desktop Only */}
          <div className="hidden md:block px-2 py-2">
            <button
              onClick={addEmptyRow}
              className="w-full md:w-auto px-4 py-2.5 bg-blue-600 text-white rounded-lg hover:bg-blue-700 flex items-center justify-center md:justify-start text-sm font-medium"
            >
              <Plus className="h-4 w-4 mr-2" />
              Add Product Row
            </button>
          </div>
        </div>

        {/* Bottom - Totals, Discount & Payment - SCROLLABLE MOBILE VERSION */}
        <div className="bg-white border-t-2 border-gray-300 p-2 flex-shrink-0 shadow-md md:static">
          {/* MOBILE: Scrollable Column Layout - Step by Step */}
          <div className="md:hidden space-y-3 max-h-[calc(100vh-400px)] overflow-y-auto pb-4">
            {/* Section 1: Totals Summary - Collapsible Style */}
            <div className="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg border-2 border-blue-300 p-3 shadow-sm">
              <h3 className="text-xs font-bold text-blue-900 mb-2 flex items-center">
                <Calculator className="h-3.5 w-3.5 mr-1.5" />
                Invoice Summary
              </h3>
              <div className="space-y-1.5 text-xs">
                <div className="flex justify-between items-center">
                  <span className="text-gray-700 font-medium">Subtotal:</span>
                  <span className="font-bold text-gray-900">AED {totals.subtotal.toFixed(2)}</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-gray-700 font-medium">VAT 5%:</span>
                  <span className="font-bold text-blue-700">AED {totals.vatTotal.toFixed(2)}</span>
                </div>
                {discount > 0 && (
                  <div className="flex justify-between items-center">
                    <span className="text-red-600 font-medium">Discount:</span>
                    <span className="font-bold text-red-600">-AED {discount.toFixed(2)}</span>
                  </div>
                )}
                <div className="flex justify-between items-center pt-2 mt-2 border-t-2 border-blue-300">
                  <span className="text-gray-900 font-bold text-sm">GRAND TOTAL:</span>
                  <span className="font-bold text-green-700 text-base">AED {totals.grandTotal.toFixed(2)}</span>
                </div>
              </div>
            </div>
            
            {/* Section 2: Discount Input */}
            <div className="bg-white rounded-lg border-2 border-gray-300 p-3">
              <label className="block text-xs font-bold text-gray-700 mb-2 flex items-center">
                <span className="bg-red-100 text-red-700 rounded-full px-2 py-0.5 text-[10px] mr-2">Optional</span>
                Discount Amount
              </label>
              <input
                type="text"
                inputMode="decimal"
                className="w-full px-3 py-2.5 border-2 border-gray-300 rounded-lg text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-red-500 focus:border-red-500"
                placeholder="Enter discount (e.g., 10.00)"
                value={discountInput}
                onChange={(e) => {
                  const value = e.target.value
                  if (value === '' || /^\d*\.?\d*$/.test(value)) {
                    setDiscountInput(value)
                    const numValue = value === '' ? 0 : parseFloat(value)
                    setDiscount(isNaN(numValue) ? 0 : numValue)
                  }
                }}
              />
            </div>
            
            {/* Section 3: Payment Details */}
            <div className="bg-white rounded-lg border-2 border-gray-300 p-3">
              <label className="block text-xs font-bold text-gray-700 mb-2">Payment Method</label>
              <select
                className="w-full px-3 py-2.5 border-2 border-gray-300 rounded-lg text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                value={paymentMethod}
                onChange={(e) => setPaymentMethod(e.target.value)}
              >
                <option value="Pending">Credit Invoice (Pay Later)</option>
                <option value="Cash">Cash Payment</option>
                <option value="Cheque">Cheque Payment</option>
                <option value="Online">Online Payment</option>
              </select>
              
              {/* Payment Amount (if not Credit) */}
              {paymentMethod !== 'Pending' && (
                <div className="mt-3">
                  <label className="block text-xs font-bold text-gray-700 mb-2">Payment Amount (AED)</label>
                  <input
                    type="number"
                    min="0"
                    step="0.01"
                    className="w-full px-3 py-2.5 border-2 border-green-300 rounded-lg text-sm font-semibold focus:outline-none focus:ring-2 focus:ring-green-500 focus:border-green-500"
                    placeholder={`Full amount: ${totals.grandTotal.toFixed(2)}`}
                    value={paymentAmount}
                    onChange={(e) => setPaymentAmount(e.target.value)}
                  />
                  <p className="text-[10px] text-gray-500 mt-1">Leave empty to use full amount</p>
                </div>
              )}
            </div>
            
            {/* Section 4: Notes (Optional) */}
            <div className="bg-white rounded-lg border-2 border-gray-300 p-3">
              <label className="block text-xs font-bold text-gray-700 mb-2 flex items-center">
                <span className="bg-gray-100 text-gray-600 rounded-full px-2 py-0.5 text-[10px] mr-2">Optional</span>
                Notes
              </label>
              <textarea
                className="w-full px-3 py-2 border-2 border-gray-300 rounded-lg text-xs focus:outline-none focus:ring-2 focus:ring-blue-500 focus:border-blue-500"
                rows="2"
                placeholder="Add notes (optional)..."
                value={notes}
                onChange={(e) => setNotes(e.target.value)}
              />
            </div>
            
            {/* Section 5: Save Button - Sticky at bottom of scrollable area */}
            <button
              onClick={handleSave}
              disabled={loading || loadingSale || cart.length === 0}
              className={`w-full px-4 py-3.5 rounded-lg font-bold text-sm shadow-lg disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center ${
                isEditMode 
                  ? 'bg-blue-600 text-white hover:bg-blue-700 active:bg-blue-800' 
                  : 'bg-green-600 text-white hover:bg-green-700 active:bg-green-800'
              }`}
            >
              {(loading || loadingSale) ? (
                <>
                  <div className="animate-spin rounded-full h-5 w-5 border-b-2 border-white mr-2"></div>
                  <span>Processing...</span>
                </>
              ) : (
                <>
                  <Save className="h-5 w-5 mr-2" />
                  <span>{isEditMode ? 'Update Invoice' : 'Save & Generate Invoice'}</span>
                </>
              )}
            </button>
          </div>
          
          {/* DESKTOP: Original 3-column Layout */}
          <div className="hidden md:grid md:grid-cols-2 lg:grid-cols-3 gap-2 sm:gap-3 lg:gap-4">
            {/* Totals Box - Compact with No Number Wrapping */}
            <div className="bg-gray-50 rounded-lg border-2 border-gray-300 p-2 sm:p-3">
              <h3 className="text-xs sm:text-sm font-bold text-gray-900 mb-1.5 sm:mb-2 border-b border-gray-400 pb-1">Totals</h3>
              <div className="space-y-1 sm:space-y-1.5">
                <div className="flex justify-between items-center">
                  <span className="text-xs font-medium text-gray-700">INV.Amount</span>
                  <span className="font-bold text-xs sm:text-sm text-gray-900 whitespace-nowrap">AED {totals.subtotal.toFixed(2)}</span>
                </div>
                <div className="flex justify-between items-center">
                  <span className="text-xs font-medium text-gray-700">VAT 5%</span>
                  <span className="font-bold text-xs sm:text-sm text-gray-900 whitespace-nowrap">AED {totals.vatTotal.toFixed(2)}</span>
                </div>
                {discount > 0 && (
                  <div className="flex justify-between items-center text-red-700">
                    <span className="text-xs font-medium">Discount</span>
                    <span className="font-bold text-xs sm:text-sm whitespace-nowrap">-AED {discount.toFixed(2)}</span>
                  </div>
                )}
                <div className="flex justify-between items-center text-xs sm:text-sm font-bold border-t border-gray-400 pt-1.5">
                  <span className="text-gray-800">Total</span>
                  <span className="text-green-700 text-sm sm:text-base whitespace-nowrap">AED {totals.grandTotal.toFixed(2)}</span>
                </div>
              </div>
              
              {/* Optional Discount Field - Compact */}
              <div className="mt-2 pt-1.5 border-t border-gray-300">
                <label className="block text-xs font-medium text-gray-700 mb-1">Discount</label>
                <input
                  type="text"
                  inputMode="decimal"
                  className="w-full px-2 py-1.5 border border-gray-300 rounded text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                  placeholder="0.00"
                  value={discountInput}
                  onChange={(e) => {
                    const value = e.target.value
                    // Allow empty, numbers, and one decimal point
                    if (value === '' || /^\d*\.?\d*$/.test(value)) {
                      setDiscountInput(value)
                      // Update numeric discount for calculations
                      const numValue = value === '' ? 0 : parseFloat(value)
                      setDiscount(isNaN(numValue) ? 0 : numValue)
                    }
                  }}
                  onBlur={() => {
                    // Format on blur: if empty, set to 0, otherwise format to 2 decimals
                    if (discountInput === '' || discountInput === '0' || discountInput === '0.') {
                      setDiscountInput('')
                      setDiscount(0)
                    } else {
                      const numValue = parseFloat(discountInput)
                      if (!isNaN(numValue)) {
                        setDiscountInput(numValue.toFixed(2))
                        setDiscount(numValue)
                      }
                    }
                  }}
                />
              </div>
            </div>

            {/* Payment Info Box - Compact */}
            <div className="bg-gray-50 rounded-lg border-2 border-gray-300 p-2 sm:p-3">
              <h3 className="text-xs sm:text-sm font-bold text-gray-900 mb-1.5 sm:mb-2 border-b border-gray-400 pb-1">
                Payment <span className="text-xs font-normal text-gray-500">(Optional)</span>
              </h3>
              <div className="space-y-1.5 sm:space-y-2">
                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Method</label>
                  <select
                    className="w-full px-2 py-1.5 border border-gray-300 rounded text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                    value={paymentMethod}
                    onChange={(e) => setPaymentMethod(e.target.value)}
                  >
                    <option value="Pending">Credit Invoice</option>
                    <option value="Cash">Cash</option>
                    <option value="Cheque">Cheque</option>
                    <option value="Online">Online</option>
                  </select>
                </div>
                
                {paymentMethod !== 'Pending' && (
                  <div>
                    <label className="block text-xs font-medium text-gray-700 mb-1">Amount</label>
                    <input
                      type="number"
                      min="0"
                      step="0.01"
                      className="w-full px-2 py-1.5 border border-gray-300 rounded text-xs sm:text-sm focus:outline-none focus:ring-2 focus:ring-blue-500"
                      placeholder="Full amount if empty"
                      value={paymentAmount}
                      onChange={(e) => setPaymentAmount(e.target.value)}
                    />
                  </div>
                )}

                <div>
                  <label className="block text-xs font-medium text-gray-700 mb-1">Notes</label>
                  <textarea
                    className="w-full px-2 py-1 border border-gray-300 rounded text-xs focus:outline-none focus:ring-2 focus:ring-blue-500"
                    rows="2"
                    placeholder="Notes..."
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                  />
                </div>
              </div>
            </div>

            {/* Save Button Box - Compact */}
            <div className="flex flex-col justify-end">
              <button
                onClick={handleSave}
                disabled={loading || loadingSale || cart.length === 0}
                className={`w-full px-3 sm:px-4 py-2.5 sm:py-3 rounded-lg font-bold text-xs sm:text-sm disabled:opacity-50 disabled:cursor-not-allowed flex items-center justify-center shadow-lg transition-colors ${
                  isEditMode 
                    ? 'bg-blue-600 text-white hover:bg-blue-700' 
                    : 'bg-green-600 text-white hover:bg-green-700'
                }`}
              >
                {(loading || loadingSale) ? (
                  <div className="animate-spin rounded-full h-3.5 w-3.5 sm:h-4 sm:w-4 border-b-2 border-white mr-1.5"></div>
                ) : (
                  <Save className="h-3.5 w-3.5 sm:h-4 sm:w-4 mr-1.5" />
                )}
                <span className="text-xs sm:text-sm">{isEditMode ? 'Update Invoice' : 'Save Invoice'}</span>
              </button>
              <p className="text-xs text-gray-500 text-center mt-1">Auto-backup enabled</p>
            </div>
          </div>
        </div>
      </div>

      {/* Customer Search Modal */}
      {showCustomerSearch && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg border-2 border-blue-300 shadow-xl w-full max-w-md">
            <div className="p-4 border-b-2 border-blue-300 bg-blue-100 flex items-center justify-between">
              <h3 className="text-lg font-bold text-gray-900">Select Customer</h3>
              <button
                onClick={() => {
                  setShowCustomerSearch(false)
                  setCustomerSearchTerm('')
                }}
                className="text-gray-500 hover:text-gray-700"
              >
                <X className="h-5 w-5" />
              </button>
            </div>
            <div className="p-4">
              <div className="relative mb-4">
                <input
                  ref={customerInputRef}
                  type="text"
                  placeholder="Search customers (F4)..."
                  className="w-full px-3 py-2 border-2 border-blue-300 rounded text-sm"
                  value={customerSearchTerm}
                  onChange={(e) => setCustomerSearchTerm(e.target.value)}
                />
                <Search className="absolute right-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
              </div>
              <div className="space-y-2 max-h-64 overflow-y-auto">
                <div
                  className="p-3 border border-blue-200 rounded-lg hover:bg-blue-50 cursor-pointer bg-blue-50"
                  onClick={() => {
                    setSelectedCustomer(null)
                    loadNextInvoiceNumber() // Load invoice number when cash customer selected
                    setShowCustomerSearch(false)
                    setCustomerSearchTerm('')
                  }}
                >
                  <p className="font-medium text-gray-900">Cash Customer</p>
                </div>
                {filteredCustomers.map((customer) => (
                  <div
                    key={customer.id}
                    className="p-3 border border-blue-200 rounded-lg hover:bg-blue-50 cursor-pointer"
                    onClick={() => {
                      setSelectedCustomer(customer)
                      loadNextInvoiceNumber() // Load invoice number when customer selected
                      setShowCustomerSearch(false)
                      setCustomerSearchTerm('')
                    }}
                  >
                    <p className="font-medium text-gray-900">{customer.name}</p>
                    <p className="text-xs text-gray-500">{customer.phone}</p>
                    {customer.address && <p className="text-xs text-gray-500">{customer.address}</p>}
                    <p className={`text-xs font-medium ${customer.balance < 0 ? 'text-green-600' : customer.balance > 0 ? 'text-red-600' : 'text-gray-600'}`}>
                      Balance: {formatBalance(customer.balance || 0)}
                    </p>
                  </div>
                ))}
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Edit Reason Modal */}
      {showEditReasonModal && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full">
            <div className="flex items-center justify-between p-6 border-b border-gray-200 bg-yellow-50">
              <div>
                <h2 className="text-xl font-bold text-gray-900 flex items-center">
                  <AlertTriangle className="h-6 w-6 text-yellow-600 mr-2" />
                  Edit Reason Required
                </h2>
                <p className="text-sm text-gray-600 mt-1">Staff users must provide a reason for editing invoices</p>
              </div>
              <button
                onClick={() => setShowEditReasonModal(false)}
                className="text-gray-400 hover:text-gray-600"
              >
                <X className="h-6 w-6" />
              </button>
            </div>
            <div className="p-6">
              <label className="block text-sm font-medium text-gray-700 mb-2">
                Reason for Editing Invoice:
              </label>
              <textarea
                value={editReason}
                onChange={(e) => setEditReason(e.target.value)}
                placeholder="Enter reason for editing this invoice (e.g., 'Wrong quantity entered', 'Customer requested change', etc.)"
                className="w-full px-4 py-3 border border-gray-300 rounded-lg focus:ring-2 focus:ring-blue-500 focus:border-blue-500 resize-none"
                rows={4}
                autoFocus
              />
              <div className="flex gap-3 mt-4">
                <button
                  onClick={async () => {
                    if (!editReason.trim()) {
                      toast.error('Please provide a reason for editing')
                      return
                    }
                    setShowEditReasonModal(false)
                    // Proceed with save - the editReason is already in state
                    // Re-trigger save by setting loading and calling the save logic
                    setLoading(true)
                    try {
                      const totals = calculateTotals()
                      const saleData = {
                        customerId: selectedCustomer?.id || null,
                        items: cart.filter(item => item.productId && item.qty > 0 && item.unitPrice > 0).map(item => ({
                          productId: item.productId,
                          unitType: item.unitType,
                          qty: Number(item.qty),
                          unitPrice: Number(item.unitPrice)
                        })),
                        discount: discount || 0,
                        payments: paymentAmount ? [{
                          method: paymentMethod,
                          amount: parseFloat(paymentAmount)
                        }] : [],
                        notes: notes || null,
                        ...(editReason && { editReason: editReason }),
                        ...(editingSale?.rowVersion && { rowVersion: editingSale.rowVersion })
                      }
                      const response = await salesAPI.updateSale(editingSaleId, saleData)
                      if (response.success) {
                        const invoiceNo = response.data?.invoiceNo
                        const saleId = response.data?.id
                        toast.success(`Invoice ${invoiceNo || editingSaleId} updated successfully!`)
                        await Promise.all([
                          loadProducts(),
                          loadCustomers(),
                          loadNextInvoiceNumber()
                        ])
                        setIsEditMode(false)
                        setEditingSaleId(null)
                        setEditingSale(null)
                        setEditReason('')
                        setSearchParams({})
                        if (saleId) {
                          setLastCreatedInvoice({
                            id: saleId,
                            invoiceNo: invoiceNo,
                            data: response.data
                          })
                          setShowInvoiceOptionsModal(true)
                        } else {
                          handleNewInvoice()
                        }
                      } else {
                        const errorMsg = response.message || response.errors?.[0] || 'Failed to update invoice'
                        toast.error(errorMsg)
                      }
                    } catch (error) {
                      console.error('Error updating invoice:', error)
                      const errorMsg = error?.response?.data?.message || 
                                      error?.response?.data?.errors?.[0] || 
                                      error?.message || 
                                      'Failed to update invoice. Please try again.'
                      toast.error(errorMsg)
                    } finally {
                      setLoading(false)
                    }
                  }}
                  className="flex-1 px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors font-medium"
                >
                  Continue
                </button>
                <button
                  onClick={() => {
                    setShowEditReasonModal(false)
                    setEditReason('')
                    setLoading(false)
                  }}
                  className="flex-1 px-6 py-3 bg-gray-300 text-gray-700 rounded-lg hover:bg-gray-400 transition-colors font-medium"
                >
                  Cancel
                </button>
              </div>
            </div>
          </div>
        </div>
      )}

      {/* Invoice Options Modal */}
      {showInvoiceOptionsModal && lastCreatedInvoice && (
        <div className="fixed inset-0 bg-black bg-opacity-50 flex items-center justify-center z-50 p-4">
          <div className="bg-white rounded-lg shadow-xl max-w-md w-full">
            {/* Header */}
            <div className="flex items-center justify-between p-6 border-b border-gray-200 bg-green-50">
              <div>
                <h2 className="text-xl font-bold text-gray-900 flex items-center">
                  <CheckCircle className="h-6 w-6 text-green-600 mr-2" />
                  Invoice Generated Successfully!
                </h2>
                <p className="text-sm text-gray-600 mt-1">Invoice: {lastCreatedInvoice.invoiceNo}</p>
              </div>
              <button
                onClick={handleCloseInvoiceOptions}
                className="text-gray-400 hover:text-gray-600"
              >
                <X className="h-6 w-6" />
              </button>
            </div>

            {/* Content */}
            <div className="p-6 space-y-4">
              <p className="text-gray-700 mb-4">What would you like to do with this invoice?</p>
              
              {/* Action Buttons */}
              <div className="space-y-3">
                <button
                  onClick={handlePrintReceipt}
                  className="w-full flex items-center justify-center px-6 py-3 bg-blue-600 text-white rounded-lg hover:bg-blue-700 transition-colors shadow-md"
                >
                  <Printer className="h-5 w-5 mr-2" />
                  Print Receipt
                </button>
                
                <button
                  onClick={() => handleDownloadPdf(lastCreatedInvoice.id, lastCreatedInvoice.invoiceNo)}
                  className="w-full flex items-center justify-center px-6 py-3 bg-gray-600 text-white rounded-lg hover:bg-gray-700 transition-colors shadow-md"
                >
                  <Download className="h-5 w-5 mr-2" />
                  Download PDF
                </button>
                
                <button
                  onClick={handleWhatsAppShare}
                  className="w-full flex items-center justify-center px-6 py-3 bg-green-600 text-white rounded-lg hover:bg-green-700 transition-colors shadow-md"
                >
                  <MessageCircle className="h-5 w-5 mr-2" />
                  Share via WhatsApp
                </button>
                
                <button
                  onClick={handleEmailShare}
                  className="w-full flex items-center justify-center px-6 py-3 bg-purple-600 text-white rounded-lg hover:bg-purple-700 transition-colors shadow-md"
                >
                  <Mail className="h-5 w-5 mr-2" />
                  Send via Email
                </button>
              </div>
            </div>

            {/* Footer */}
            <div className="flex items-center justify-end p-6 border-t border-gray-200 bg-gray-50">
              <button
                onClick={handleCloseInvoiceOptions}
                className="px-6 py-2 text-gray-700 bg-white border border-gray-300 rounded-md hover:bg-gray-50"
              >
                Done
              </button>
            </div>
          </div>
        </div>
      )}
    </div>
  )
}

export default PosPage
