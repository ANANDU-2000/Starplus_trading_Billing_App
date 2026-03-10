import { useState, useEffect, useCallback } from 'react'
import { useForm } from 'react-hook-form'
import { 
  Plus, 
  Search, 
  Filter, 
  RefreshCw,
  DollarSign,
  Calendar,
  Tag,
  Edit,
  Trash2,
  TrendingDown,
  PieChart,
  X,
  Save
} from 'lucide-react'
import { formatCurrency } from '../utils/currency'
import toast from 'react-hot-toast'
import { LoadingCard, LoadingButton } from '../components/Loading'
import { Input, Select, TextArea } from '../components/Form'
import Modal from '../components/Modal'
import BulkVatUpdateModal from '../components/BulkVatUpdateModal'
import { expensesAPI } from '../services'
import { 
  PieChart as RechartsPieChart,
  Pie,
  Cell,
  ResponsiveContainer,
  Tooltip
} from 'recharts'

const ExpensesPage = () => {
  const [loading, setLoading] = useState(true)
  const [expenses, setExpenses] = useState([])
  const [filteredExpenses, setFilteredExpenses] = useState([])
  const [searchTerm, setSearchTerm] = useState('')
  const [showAddModal, setShowAddModal] = useState(false)
  const [showEditModal, setShowEditModal] = useState(false)
  const [selectedExpense, setSelectedExpense] = useState(null)
  const [creatingCategory, setCreatingCategory] = useState(false)
  const [expenseSummary, setExpenseSummary] = useState(null)
  const [currentPage, setCurrentPage] = useState(1)
  const [totalPages, setTotalPages] = useState(1)
  const [dateRange, setDateRange] = useState({
    from: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString().split('T')[0],
    to: new Date().toISOString().split('T')[0]
  })
  const [groupBy, setGroupBy] = useState('') // '', 'weekly', 'monthly', 'yearly'
  const [showAggregated, setShowAggregated] = useState(false)
  const [aggregatedData, setAggregatedData] = useState([])
  const [noVatOnly, setNoVatOnly] = useState(false)
  const [noVatCount, setNoVatCount] = useState(0)
  const [showBulkVatModal, setShowBulkVatModal] = useState(false)
  const [selectedExpenseIds, setSelectedExpenseIds] = useState([])
  const [showCategoryVatModal, setShowCategoryVatModal] = useState(false)
  const [updatingCategoryId, setUpdatingCategoryId] = useState(null)
  const [categoryVatEdits, setCategoryVatEdits] = useState({})
  
  const [categories, setCategories] = useState([]) // full list { id, name, colorCode, defaultVatRate, ... }

  const {
    register,
    handleSubmit,
    reset,
    setValue,
    watch,
    formState: { errors }
  } = useForm()
  const watchedCategoryId = watch('category')
  const selectedCategory = categories.find((c) => String(c.id) === String(watchedCategoryId))
  const categoryVatLocked = selectedCategory?.vatDefaultLocked ?? false

  const fetchCategories = useCallback(async () => {
    try {
      const response = await expensesAPI.getExpenseCategories()
      if (response?.success && response?.data && Array.isArray(response.data)) {
        setCategories(response.data)
      } else {
        setCategories([])
      }
    } catch (error) {
      console.error('Failed to load categories:', error)
      toast.error(error?.response?.data?.message || 'Failed to load expense categories')
      setCategories([])
    }
  }, [])

  const categoryOptions = categories.map(cat => ({
    value: cat.id,
    label: cat.defaultVatRate != null && cat.defaultVatRate > 0
      ? `${cat.name} (${(cat.defaultVatRate * 100)}% VAT${cat.defaultIsTaxClaimable ? ', Claimable' : ''})`
      : cat.name,
    color: cat.colorCode
  }))


  const fetchExpenses = useCallback(async () => {
    try {
      setLoading(true)
      const params = {
        page: currentPage,
        pageSize: 10,
        fromDate: dateRange.from,
        toDate: dateRange.to,
        ...(noVatOnly ? { noVatOnly: true } : {})
      }
      
      // Fetch aggregated view if enabled
      if (showAggregated && groupBy) {
        try {
          const aggResponse = await expensesAPI.getExpensesAggregated({
            fromDate: dateRange.from,
            toDate: dateRange.to,
            groupBy: groupBy
          })
          if (aggResponse?.success && aggResponse?.data) {
            setAggregatedData(aggResponse.data)
          } else {
            // Handle case where no data is returned
            setAggregatedData([])
            if (aggResponse?.message) {
              console.warn('Aggregated expenses warning:', aggResponse.message)
            }
          }
        } catch (error) {
          console.error('Error loading aggregated expenses:', error)
          const errorMessage = error?.response?.data?.message || error?.message || 'Failed to load aggregated expenses'
          toast.error(errorMessage)
          setAggregatedData([])
          // Don't fail the entire fetch if aggregated view fails
        }
      }
      
      const response = await expensesAPI.getExpenses(params)
      if (response?.success && response?.data) {
        const expenseList = response.data.items || []
        setExpenses(expenseList)
        setFilteredExpenses(expenseList)
        setTotalPages(response.data.totalPages || 1)
        if (noVatOnly) setNoVatCount(response.data.totalCount ?? 0)
        
        const total = expenseList.reduce((sum, expense) => sum + (expense.totalAmount ?? expense.amount ?? 0), 0)
        const categoryTotals = expenseList.reduce((acc, expense) => {
          const cat = expense.categoryName || 'Other'
          acc[cat] = (acc[cat] || 0) + (expense.totalAmount ?? expense.amount ?? 0)
          return acc
        }, {})
        
        setExpenseSummary({
          total,
          categoryTotals,
          averagePerDay: total / 30,
          topCategory: Object.keys(categoryTotals).length > 0 
            ? Object.keys(categoryTotals).reduce((a, b) => 
                categoryTotals[a] > categoryTotals[b] ? a : b
              )
            : 'N/A'
        })
      } else {
        setExpenses([])
        setFilteredExpenses([])
        setTotalPages(1)
        setExpenseSummary(null)
      }
    } catch (error) {
      console.error('Error loading expenses:', error)
      toast.error(error?.response?.data?.message || 'Failed to load expenses')
      setExpenses([])
      setFilteredExpenses([])
      setExpenseSummary(null)
    } finally {
      setLoading(false)
    }
  }, [currentPage, dateRange, showAggregated, groupBy, noVatOnly])

  const filterExpenses = useCallback(() => {
    if (!searchTerm) {
      setFilteredExpenses(expenses)
      return
    }

    const filtered = expenses.filter(expense =>
      expense.categoryName?.toLowerCase().includes(searchTerm.toLowerCase()) ||
      expense.note?.toLowerCase().includes(searchTerm.toLowerCase())
    )
    setFilteredExpenses(filtered)
  }, [expenses, searchTerm])

  useEffect(() => {
    fetchCategories()
    fetchExpenses()
  }, [fetchCategories, fetchExpenses])

  useEffect(() => {
    if (!noVatOnly) {
      expensesAPI.getExpenses({ noVatOnly: true, pageSize: 1, page: 1 }).then((r) => {
        if (r?.success && r?.data) setNoVatCount(r.data.totalCount ?? 0)
      }).catch(() => setNoVatCount(0))
    }
  }, [noVatOnly])

  useEffect(() => {
    if (showCategoryVatModal && categories.length > 0) {
      setCategoryVatEdits(categories.reduce((acc, c) => ({
        ...acc,
        [c.id]: {
          defaultVatRate: c.defaultVatRate ?? 0,
          defaultTaxType: c.defaultTaxType ?? 'Standard',
          defaultIsTaxClaimable: c.defaultIsTaxClaimable ?? false,
          defaultIsEntertainment: c.defaultIsEntertainment ?? false,
          vatDefaultLocked: c.vatDefaultLocked ?? false
        }
      }), {}))
    }
  }, [showCategoryVatModal, categories])

  useEffect(() => {
    filterExpenses()
  }, [filterExpenses])

  useEffect(() => {
    if (selectedCategory && (showAddModal || showEditModal)) {
      setValue('vatRate', selectedCategory.defaultVatRate ?? 0)
      setValue('taxType', selectedCategory.defaultTaxType ?? 'Standard')
      setValue('isTaxClaimable', selectedCategory.defaultIsTaxClaimable ?? false)
      setValue('isEntertainment', selectedCategory.defaultIsEntertainment ?? false)
    }
  }, [watchedCategoryId, selectedCategory, showAddModal, showEditModal, setValue])

  const onSubmit = async (data) => {
    try {
      const expenseDate = data.date ? new Date(data.date).toISOString() : new Date().toISOString()
      const payload = {
        categoryId: parseInt(data.category),
        amount: parseFloat(data.amount),
        date: expenseDate,
        note: data.note || ''
      }
      if (!categoryVatLocked) {
        if (data.vatRate != null && data.vatRate !== '') payload.vatRate = parseFloat(data.vatRate)
        if (data.taxType) payload.taxType = data.taxType
        if (data.isTaxClaimable !== undefined) payload.isTaxClaimable = !!data.isTaxClaimable
        if (data.isEntertainment !== undefined) payload.isEntertainment = !!data.isEntertainment
      }
      if (selectedExpense) {
        const response = await expensesAPI.updateExpense(selectedExpense.id, payload)
        
        if (response?.success) {
          toast.success('Expense updated successfully!')
        } else {
          toast.error(response?.message || 'Failed to update expense')
          return
        }
      } else {
        const response = await expensesAPI.createExpense(payload)
        
        if (response?.success) {
          toast.success('Expense added successfully!')
        } else {
          toast.error(response?.message || 'Failed to create expense')
          return
        }
      }
      
      reset()
      setShowAddModal(false)
      setShowEditModal(false)
      setSelectedExpense(null)
      setCurrentPage(1)
      fetchExpenses()
    } catch (error) {
      console.error('Error saving expense:', error)
      toast.error(error?.response?.data?.message || 'Failed to save expense')
    }
  }

  const handleEdit = (expense) => {
    setSelectedExpense(expense)
    setValue('category', expense.categoryId || '')
    setValue('amount', expense.amount ?? 0)
    const expenseDate = expense.date
      ? new Date(expense.date).toISOString().split('T')[0]
      : new Date().toISOString().split('T')[0]
    setValue('date', expenseDate)
    setValue('note', expense.note || '')
    setValue('vatRate', expense.vatRate ?? '')
    setValue('taxType', expense.taxType || 'Standard')
    setValue('isTaxClaimable', expense.isTaxClaimable ?? false)
    setValue('isEntertainment', expense.isEntertainment ?? false)
    setShowEditModal(true)
  }

  const handleDelete = async (expenseId) => {
    if (!window.confirm('Are you sure you want to delete this expense?')) return
    
    try {
      const response = await expensesAPI.deleteExpense(expenseId)
      
      if (response?.success) {
        toast.success('Expense deleted successfully!')
        fetchExpenses()
      } else {
        toast.error(response?.message || 'Failed to delete expense')
      }
    } catch (error) {
      console.error('Error deleting expense:', error)
      toast.error(error?.response?.data?.message || 'Failed to delete expense')
    }
  }

  const getCategoryColor = (category) => {
    const colors = {
      'Rent': '#EF4444',
      'Utilities': '#F59E0B',
      'Staff Salary': '#10B981',
      'Marketing': '#3B82F6',
      'Fuel': '#8B5CF6',
      'Delivery': '#F97316',
      'Food': '#EC4899',
      'Maintenance': '#6B7280',
      'Insurance': '#14B8A6',
      'Other': '#84CC16'
    }
    return colors[category] || '#6B7280'
  }

  const chartData = expenseSummary ? Object.entries(expenseSummary.categoryTotals).map(([category, amount]) => ({
    name: category,
    value: amount,
    color: getCategoryColor(category)
  })) : []

  if (loading) {
    return <LoadingCard message="Loading expenses..." />
  }

  // TALLY ERP LEDGER STYLE
  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-slate-50">
      {/* Top Bar - Mobile Responsive */}
      <div className="bg-blue-100 border-b-2 border-blue-200 px-2 sm:px-4 py-2">
        <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-2 sm:gap-0">
          <div>
            <h1 className="text-base sm:text-lg font-bold text-gray-900">Expenses Ledger</h1>
            <div className="text-xs text-gray-600">Date: {new Date().toLocaleDateString('en-GB')}</div>
          </div>
          <div className="flex items-center gap-2 w-full sm:w-auto">
            <button
              onClick={fetchExpenses}
              className="px-2 sm:px-3 py-1 text-xs font-medium bg-white border border-blue-300 rounded hover:bg-blue-50 flex items-center justify-center flex-1 sm:flex-none"
            >
              <RefreshCw className="h-3.5 w-3.5 sm:h-4 sm:w-4 sm:mr-1" />
              <span className="hidden sm:inline">Refresh</span>
            </button>
            <button
              onClick={() => setShowAddModal(true)}
              className="px-2 sm:px-3 lg:px-4 py-1.5 sm:py-2 bg-green-600 text-white rounded font-medium hover:bg-green-700 flex items-center justify-center text-xs sm:text-sm flex-1 sm:flex-none"
            >
              <Plus className="h-3.5 w-3.5 sm:h-4 sm:w-4 sm:mr-2" />
              <span className="hidden sm:inline">Add Expense</span>
              <span className="sm:hidden">Add</span>
            </button>
          </div>
        </div>
      </div>

      <div className="p-2 sm:p-4">
        {/* Filters */}
        <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-3 sm:p-4 mb-4">
          <div className="flex items-center mb-3">
            <Filter className="h-4 w-4 text-blue-600 mr-2" />
            <h3 className="text-sm font-semibold text-gray-900">Filters</h3>
          </div>
          
          {/* Date Range Presets */}
          <div className="mb-3 flex flex-wrap gap-2">
            <button
              onClick={() => {
                const to = new Date().toISOString().split('T')[0]
                const from = new Date()
                from.setDate(from.getDate() - 7)
                setDateRange({ from: from.toISOString().split('T')[0], to })
              }}
              className="px-2 py-1 text-xs bg-blue-50 text-blue-700 rounded hover:bg-blue-100"
            >
              Last 7 Days
            </button>
            <button
              onClick={() => {
                const to = new Date()
                const from = new Date(to)
                from.setDate(from.getDate() - from.getDay()) // Start of week
                setDateRange({ from: from.toISOString().split('T')[0], to: to.toISOString().split('T')[0] })
              }}
              className="px-2 py-1 text-xs bg-blue-50 text-blue-700 rounded hover:bg-blue-100"
            >
              This Week
            </button>
            <button
              onClick={() => {
                const to = new Date().toISOString().split('T')[0]
                const from = new Date()
                from.setDate(1) // First day of month
                setDateRange({ from: from.toISOString().split('T')[0], to })
              }}
              className="px-2 py-1 text-xs bg-blue-50 text-blue-700 rounded hover:bg-blue-100"
            >
              This Month
            </button>
            <button
              onClick={() => {
                const to = new Date().toISOString().split('T')[0]
                const from = new Date()
                from.setFullYear(from.getFullYear(), 0, 1) // First day of year
                setDateRange({ from: from.toISOString().split('T')[0], to })
              }}
              className="px-2 py-1 text-xs bg-blue-50 text-blue-700 rounded hover:bg-blue-100"
            >
              This Year
            </button>
          </div>
          
          <div className="flex flex-wrap gap-2 mb-3 items-center">
            <span className="text-sm font-medium text-gray-700">VAT filter:</span>
            <button
              type="button"
              onClick={() => setNoVatOnly(false)}
              className={`px-2 py-1 text-xs rounded ${!noVatOnly ? 'bg-blue-600 text-white' : 'bg-blue-50 text-blue-700 hover:bg-blue-100'}`}
            >
              All
            </button>
            <button
              type="button"
              onClick={() => setNoVatOnly(true)}
              className={`px-2 py-1 text-xs rounded ${noVatOnly ? 'bg-amber-600 text-white' : 'bg-amber-50 text-amber-700 hover:bg-amber-100'}`}
            >
              No VAT data
            </button>
            <button
              type="button"
              onClick={() => setShowCategoryVatModal(true)}
              className="ml-2 px-2 py-1 text-xs bg-slate-100 text-slate-700 rounded hover:bg-slate-200"
            >
              Category VAT defaults
            </button>
          </div>
          <div className="grid grid-cols-1 sm:grid-cols-3 gap-3">
            <Input
              label="From Date"
              type="date"
              value={dateRange.from}
              onChange={(e) => setDateRange(prev => ({ ...prev, from: e.target.value }))}
            />
            <Input
              label="To Date"
              type="date"
              value={dateRange.to}
              onChange={(e) => setDateRange(prev => ({ ...prev, to: e.target.value }))}
            />
            <div className="flex items-end gap-2">
              <Select
                label="Group By"
                options={[
                  { value: '', label: 'None' },
                  { value: 'weekly', label: 'Weekly' },
                  { value: 'monthly', label: 'Monthly' },
                  { value: 'yearly', label: 'Yearly' }
                ]}
                value={groupBy}
                onChange={(e) => {
                  setGroupBy(e.target.value)
                  setShowAggregated(e.target.value !== '')
                }}
              />
            </div>
          </div>
        </div>

        {noVatCount > 0 && (
          <div className="mb-4 p-3 bg-amber-50 border border-amber-200 rounded-lg flex flex-wrap items-center gap-2">
            <span className="text-sm text-amber-800">
              {noVatCount} expense(s) have no VAT data.
            </span>
            <button
              type="button"
              onClick={() => setShowBulkVatModal(true)}
              className="px-3 py-1.5 text-sm font-medium bg-amber-600 text-white rounded hover:bg-amber-700"
            >
              Review & Update
            </button>
          </div>
        )}
        
        {/* Summary Cards - Mobile Responsive */}
        {expenseSummary && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-3 sm:gap-4 mb-4 sm:mb-6">
            <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-3 sm:p-4">
              <div className="flex items-center">
                <TrendingDown className="h-5 w-5 sm:h-6 sm:w-6 lg:h-8 lg:w-8 text-red-600 flex-shrink-0" />
                <div className="ml-2 sm:ml-3 lg:ml-4 min-w-0">
                  <p className="text-xs sm:text-sm font-medium text-red-600">Total Expenses</p>
                  <p className="text-base sm:text-xl lg:text-2xl font-bold text-red-900 truncate">
                    {formatCurrency(expenseSummary.total)}
                  </p>
                </div>
              </div>
            </div>
            
            <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-3 sm:p-4">
              <div className="flex items-center">
                <Calendar className="h-5 w-5 sm:h-6 sm:w-6 lg:h-8 lg:w-8 text-blue-600 flex-shrink-0" />
                <div className="ml-2 sm:ml-3 lg:ml-4 min-w-0">
                  <p className="text-xs sm:text-sm font-medium text-blue-600">Average per Day</p>
                  <p className="text-base sm:text-xl lg:text-2xl font-bold text-blue-900 truncate">
                    {formatCurrency(expenseSummary.averagePerDay)}
                  </p>
                </div>
              </div>
            </div>
            
            <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-3 sm:p-4">
              <div className="flex items-center">
                <Tag className="h-5 w-5 sm:h-6 sm:w-6 lg:h-8 lg:w-8 text-green-600 flex-shrink-0" />
                <div className="ml-2 sm:ml-3 lg:ml-4 min-w-0">
                  <p className="text-xs sm:text-sm font-medium text-green-600">Top Category</p>
                  <p className="text-base sm:text-xl lg:text-2xl font-bold text-green-900 truncate">
                    {expenseSummary.topCategory}
                  </p>
                </div>
              </div>
            </div>
          </div>
        )}

        {/* Chart - Tally Style */}
        {chartData.length > 0 && (
          <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-4 mb-6">
            <div className="flex items-center mb-4 border-b-2 border-lime-400 pb-2">
              <PieChart className="h-6 w-6 text-blue-600 mr-2" />
              <h3 className="text-lg font-semibold text-gray-900">Expense Breakdown</h3>
            </div>
            <div className="h-64">
              <ResponsiveContainer width="100%" height="100%">
                <RechartsPieChart>
                  <Pie
                    data={chartData}
                    cx="50%"
                    cy="50%"
                    labelLine={false}
                    label={({ name, percent }) => `${name} ${(percent * 100).toFixed(0)}%`}
                    outerRadius={80}
                    fill="#8884d8"
                    dataKey="value"
                  >
                    {chartData.map((entry, index) => (
                      <Cell key={`cell-${index}`} fill={entry.color} />
                    ))}
                  </Pie>
                  <Tooltip formatter={(value) => formatCurrency(value)} />
                </RechartsPieChart>
              </ResponsiveContainer>
            </div>
          </div>
        )}

        {/* Search and Filters - Tally Style */}
        <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm p-4 mb-6">
          <div className="flex flex-col sm:flex-row gap-4">
            <div className="flex-1">
              <div className="relative">
                <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-gray-400" />
                <input
                  type="text"
                  placeholder="Search expenses..."
                  value={searchTerm}
                  onChange={(e) => setSearchTerm(e.target.value)}
                  className="pl-10 pr-4 py-2 w-full border-2 border-lime-300 rounded-md focus:ring-2 focus:ring-lime-400 focus:border-lime-400 text-sm"
                />
              </div>
            </div>
          </div>
        </div>

        {/* Aggregated View */}
        {showAggregated && aggregatedData.length > 0 && (
          <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm overflow-hidden mb-6">
            <div className="p-3 border-b-2 border-lime-400 bg-lime-100">
              <h3 className="text-sm font-bold text-gray-900">
                Expenses Aggregated by {groupBy.charAt(0).toUpperCase() + groupBy.slice(1)}
              </h3>
            </div>
            <div className="overflow-x-auto">
              <table className="min-w-full text-xs">
                <thead className="bg-lime-100">
                  <tr>
                    <th className="px-4 py-3 text-left font-semibold text-gray-700 border-r border-lime-300">Period</th>
                    <th className="px-4 py-3 text-right font-semibold text-gray-700 border-r border-lime-300">Total Amount</th>
                    <th className="px-4 py-3 text-center font-semibold text-gray-700 border-r border-lime-300">Count</th>
                    <th className="px-4 py-3 text-left font-semibold text-gray-700">By Category</th>
                  </tr>
                </thead>
                <tbody className="divide-y divide-lime-200">
                  {aggregatedData.map((agg, idx) => (
                    <tr key={idx} className="hover:bg-lime-50">
                      <td className="px-4 py-4 whitespace-nowrap font-medium text-gray-900">
                        {agg.period}
                      </td>
                      <td className="px-4 py-4 whitespace-nowrap text-right font-bold text-gray-900">
                        {formatCurrency(agg.totalAmount || 0)}
                      </td>
                      <td className="px-4 py-4 whitespace-nowrap text-center text-gray-600">
                        {agg.count || 0}
                      </td>
                      <td className="px-4 py-4">
                        <div className="space-y-1">
                          {agg.byCategory && agg.byCategory.length > 0 ? (
                            agg.byCategory.map((cat, catIdx) => (
                              <div key={catIdx} className="flex justify-between text-xs">
                                <span className="text-gray-700">{cat.categoryName}:</span>
                                <span className="font-medium text-gray-900 ml-2">
                                  {formatCurrency(cat.totalAmount || 0)} ({cat.count || 0})
                                </span>
                              </div>
                            ))
                          ) : (
                            <span className="text-gray-500 text-xs">No categories</span>
                          )}
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          </div>
        )}

        {/* Expenses Table - Tally Ledger Style */}
        {!showAggregated && (
          <div className="bg-white rounded-lg border-2 border-lime-300 shadow-sm overflow-hidden">
            <div className="p-3 border-b-2 border-lime-400 bg-lime-100 flex flex-wrap items-center justify-between gap-2">
              <h3 className="text-sm font-bold text-gray-900">Expenses Ledger</h3>
              {noVatOnly && selectedExpenseIds.length > 0 && (
                <div className="flex items-center gap-2">
                  <span className="text-xs text-gray-700">{selectedExpenseIds.length} selected</span>
                  <button type="button" onClick={() => setShowBulkVatModal(true)} className="px-2 py-1 text-xs font-medium bg-amber-600 text-white rounded hover:bg-amber-700">Apply VAT</button>
                  <button type="button" onClick={() => setSelectedExpenseIds([])} className="px-2 py-1 text-xs border border-lime-400 rounded hover:bg-lime-100">Clear</button>
                </div>
              )}
            </div>
          <div className="overflow-x-auto">
            <table className="min-w-full text-xs">
              <thead className="bg-lime-100">
                <tr>
                  {noVatOnly && (
                    <th className="px-2 py-3 text-center font-semibold text-gray-700 border-r border-lime-300 w-10">
                      <input type="checkbox" checked={filteredExpenses.length > 0 && filteredExpenses.every((e) => selectedExpenseIds.includes(e.id))} onChange={(e) => setSelectedExpenseIds(e.target.checked ? filteredExpenses.map((e) => e.id) : [])} className="rounded" />
                    </th>
                  )}
                  <th className="px-4 py-3 text-left font-semibold text-gray-700 border-r border-lime-300">Category</th>
                  <th className="px-4 py-3 text-right font-semibold text-gray-700 border-r border-lime-300">Amount</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-700 border-r border-lime-300">Date</th>
                  <th className="px-4 py-3 text-left font-semibold text-gray-700">Note</th>
                  <th className="px-4 py-3 text-center font-semibold text-gray-700">Actions</th>
                </tr>
              </thead>
              <tbody className="divide-y divide-lime-200">
                {filteredExpenses.length === 0 ? (
                  <tr>
                    <td colSpan={noVatOnly ? 6 : 5} className="px-6 py-8 text-center text-gray-500">
                      No expenses found
                    </td>
                  </tr>
                ) : (
                  filteredExpenses.map((expense) => (
                  <tr key={expense.id} className="hover:bg-lime-50">
                    {noVatOnly && (
                      <td className="px-2 py-4 text-center border-r border-lime-200">
                        <input type="checkbox" checked={selectedExpenseIds.includes(expense.id)} onChange={() => setSelectedExpenseIds((prev) => prev.includes(expense.id) ? prev.filter((id) => id !== expense.id) : [...prev, expense.id])} className="rounded" />
                      </td>
                    )}
                    <td className="px-4 py-4 whitespace-nowrap">
                      <div className="flex items-center">
                        <div 
                          className="w-3 h-3 rounded-full mr-3"
                          style={{ backgroundColor: expense.categoryColor || '#6B7280' }}
                        />
                        <span className="font-medium text-gray-900">{expense.categoryName}</span>
                      </div>
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap text-right font-medium text-gray-900">
                      {formatCurrency(expense.totalAmount ?? expense.amount)}
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap text-gray-900">
                      {expense.date ? new Date(expense.date).toLocaleDateString('en-GB') : '-'}
                    </td>
                    <td className="px-4 py-4 text-gray-900">
                      {expense.note || '-'}
                    </td>
                    <td className="px-4 py-4 whitespace-nowrap text-center space-x-2">
                      <button
                        onClick={() => handleEdit(expense)}
                        className="text-indigo-600 hover:text-indigo-900"
                      >
                        <Edit className="h-4 w-4" />
                      </button>
                      <button
                        onClick={() => handleDelete(expense.id)}
                        className="text-red-600 hover:text-red-900"
                      >
                        <Trash2 className="h-4 w-4" />
                      </button>
                    </td>
                  </tr>
                  ))
                )}
              </tbody>
            </table>
          </div>
          
          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex justify-center mt-4 pb-4">
              <div className="flex space-x-2">
                <button
                  onClick={() => setCurrentPage(Math.max(1, currentPage - 1))}
                  disabled={currentPage === 1}
                  className="px-3 py-1 border-2 border-lime-300 rounded text-xs disabled:opacity-50"
                >
                  Previous
                </button>
                <span className="flex items-center px-4 text-xs">
                  Page {currentPage} of {totalPages}
                </span>
                <button
                  onClick={() => setCurrentPage(Math.min(totalPages, currentPage + 1))}
                  disabled={currentPage === totalPages}
                  className="px-3 py-1 border-2 border-lime-300 rounded text-xs disabled:opacity-50"
                >
                  Next
                </button>
              </div>
            </div>
          )}
          </div>
        )}
      </div>

      {/* Add Expense Modal - Tally Style */}
      <Modal
        isOpen={showAddModal}
        onClose={() => {
          setShowAddModal(false)
          reset()
        }}
        title="Add New Expense"
        size="md"
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
          <div className="space-y-4">
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">
                  Category <span className="text-red-500">*</span>
                </label>
                <button
                  type="button"
                  onClick={async () => {
                    const categoryName = prompt('Enter new category name:')
                    if (categoryName && categoryName.trim()) {
                      try {
                        setCreatingCategory(true)
                        const response = await expensesAPI.createCategory({
                          name: categoryName.trim(),
                          colorCode: '#3B82F6'
                        })
                        if (response?.success) {
                          toast.success('Category created successfully!')
                          await fetchCategories()
                          setValue('category', response.data.id.toString())
                        } else {
                          toast.error(response?.message || 'Failed to create category')
                        }
                      } catch (error) {
                        console.error('Error creating category:', error)
                        toast.error(error?.response?.data?.message || 'Failed to create category')
                      } finally {
                        setCreatingCategory(false)
                      }
                    }
                  }}
                  disabled={creatingCategory}
                  className="text-xs text-blue-600 hover:text-blue-800 font-medium flex items-center gap-1 disabled:opacity-50"
                  title="Create new category"
                >
                  <Plus className="h-3 w-3" />
                  {creatingCategory ? 'Creating...' : 'New Category'}
                </button>
              </div>
              <Select
                options={categoryOptions}
                required
                error={errors.category?.message}
                {...register('category', { required: 'Category is required' })}
              />
            </div>

            <Input
              label="Amount"
              type="number"
              step="0.01"
              placeholder="0.00"
              required
              error={errors.amount?.message}
              {...register('amount', { 
                required: 'Amount is required',
                min: { value: 0.01, message: 'Amount must be greater than 0' }
              })}
            />

            <Input
              label="Date"
              type="date"
              required
              error={errors.date?.message}
              {...register('date', { required: 'Date is required' })}
            />

            <TextArea
              label="Note"
              placeholder="Expense description..."
              rows={3}
              error={errors.note?.message}
              {...register('note')}
            />

            {watchedCategoryId && (
              categoryVatLocked ? (
                <p className="text-sm text-gray-600 bg-blue-50 px-3 py-2 rounded">
                  VAT auto-set from category default (e.g. {(selectedCategory?.defaultVatRate ?? 0) * 100}%, {selectedCategory?.defaultIsTaxClaimable ? 'Claimable' : 'Non-claimable'})
                </p>
              ) : (
                <div className="grid grid-cols-2 gap-3 border-t border-lime-200 pt-3">
                  <Input label="VAT Rate (0–1)" type="number" step="0.01" min="0" max="1" {...register('vatRate')} />
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Tax Type</label>
                    <select {...register('taxType')} className="w-full border-2 border-lime-300 rounded px-3 py-2 text-sm">
                      <option value="Standard">Standard</option>
                      <option value="Exempt">Exempt</option>
                      <option value="OutOfScope">Out of Scope</option>
                      <option value="Petroleum">Petroleum</option>
                    </select>
                  </div>
                  <label className="flex items-center gap-2 col-span-2">
                    <input type="checkbox" {...register('isTaxClaimable')} />
                    <span className="text-sm text-gray-700">ITC Claimable</span>
                  </label>
                  <label className="flex items-center gap-2 col-span-2">
                    <input type="checkbox" {...register('isEntertainment')} />
                    <span className="text-sm text-gray-700">Entertainment</span>
                  </label>
                </div>
              )
            )}
          </div>

          <div className="flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => {
                setShowAddModal(false)
                reset()
              }}
              className="px-4 py-2 border-2 border-lime-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-lime-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-green-600 text-white rounded-md text-sm font-medium hover:bg-green-700 flex items-center"
            >
              <Save className="h-4 w-4 mr-2" />
              Add Expense
            </button>
          </div>
        </form>
      </Modal>

      {/* Edit Expense Modal */}
      <Modal
        isOpen={showEditModal}
        onClose={() => {
          setShowEditModal(false)
          setSelectedExpense(null)
          reset()
        }}
        title="Edit Expense"
        size="md"
      >
        <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
          <div className="space-y-4">
            <div>
              <div className="flex items-center justify-between mb-1">
                <label className="block text-sm font-medium text-gray-700">
                  Category <span className="text-red-500">*</span>
                </label>
                <button
                  type="button"
                  onClick={async () => {
                    const categoryName = prompt('Enter new category name:')
                    if (categoryName && categoryName.trim()) {
                      try {
                        setCreatingCategory(true)
                        const response = await expensesAPI.createCategory({
                          name: categoryName.trim(),
                          colorCode: '#3B82F6'
                        })
                        if (response?.success) {
                          toast.success('Category created successfully!')
                          await fetchCategories()
                          setValue('category', response.data.id.toString())
                        } else {
                          toast.error(response?.message || 'Failed to create category')
                        }
                      } catch (error) {
                        console.error('Error creating category:', error)
                        toast.error(error?.response?.data?.message || 'Failed to create category')
                      } finally {
                        setCreatingCategory(false)
                      }
                    }
                  }}
                  disabled={creatingCategory}
                  className="text-xs text-blue-600 hover:text-blue-800 font-medium flex items-center gap-1 disabled:opacity-50"
                  title="Create new category"
                >
                  <Plus className="h-3 w-3" />
                  {creatingCategory ? 'Creating...' : 'New Category'}
                </button>
              </div>
              <Select
                options={categoryOptions}
                required
                error={errors.category?.message}
                {...register('category', { required: 'Category is required' })}
              />
            </div>

            <Input
              label="Amount"
              type="number"
              step="0.01"
              placeholder="0.00"
              required
              error={errors.amount?.message}
              {...register('amount', { 
                required: 'Amount is required',
                min: { value: 0.01, message: 'Amount must be greater than 0' }
              })}
            />

            <Input
              label="Date"
              type="date"
              required
              error={errors.date?.message}
              {...register('date', { required: 'Date is required' })}
            />

            <TextArea
              label="Note"
              placeholder="Expense description..."
              rows={3}
              error={errors.note?.message}
              {...register('note')}
            />

            {selectedExpense?.vatRate == null && (
              <div className="p-3 bg-amber-50 border border-amber-200 rounded flex flex-wrap items-center gap-2">
                <span className="text-sm text-amber-800">No VAT data.</span>
                <button type="button" onClick={() => { setSelectedExpenseIds([selectedExpense.id]); setShowBulkVatModal(true) }} className="px-2 py-1 text-sm bg-amber-600 text-white rounded hover:bg-amber-700">Add 5% VAT</button>
                <span className="text-xs text-amber-700">or keep as no VAT and save.</span>
              </div>
            )}

            {watchedCategoryId && (
              categoryVatLocked ? (
                <p className="text-sm text-gray-600 bg-blue-50 px-3 py-2 rounded">
                  VAT auto-set from category default (e.g. {(selectedCategory?.defaultVatRate ?? 0) * 100}%, {selectedCategory?.defaultIsTaxClaimable ? 'Claimable' : 'Non-claimable'})
                </p>
              ) : (
                <div className="grid grid-cols-2 gap-3 border-t border-lime-200 pt-3">
                  <Input label="VAT Rate (0–1)" type="number" step="0.01" min="0" max="1" {...register('vatRate')} />
                  <div>
                    <label className="block text-sm font-medium text-gray-700 mb-1">Tax Type</label>
                    <select {...register('taxType')} className="w-full border-2 border-lime-300 rounded px-3 py-2 text-sm">
                      <option value="Standard">Standard</option>
                      <option value="Exempt">Exempt</option>
                      <option value="OutOfScope">Out of Scope</option>
                      <option value="Petroleum">Petroleum</option>
                    </select>
                  </div>
                  <label className="flex items-center gap-2 col-span-2">
                    <input type="checkbox" {...register('isTaxClaimable')} />
                    <span className="text-sm text-gray-700">ITC Claimable</span>
                  </label>
                  <label className="flex items-center gap-2 col-span-2">
                    <input type="checkbox" {...register('isEntertainment')} />
                    <span className="text-sm text-gray-700">Entertainment</span>
                  </label>
                </div>
              )
            )}
          </div>

          <div className="flex justify-end space-x-3">
            <button
              type="button"
              onClick={() => {
                setShowEditModal(false)
                setSelectedExpense(null)
                reset()
              }}
              className="px-4 py-2 border-2 border-lime-300 rounded-md text-sm font-medium text-gray-700 bg-white hover:bg-lime-50"
            >
              Cancel
            </button>
            <button
              type="submit"
              className="px-4 py-2 bg-green-600 text-white rounded-md text-sm font-medium hover:bg-green-700 flex items-center"
            >
              <Save className="h-4 w-4 mr-2" />
              Update Expense
            </button>
          </div>
        </form>
      </Modal>

      <Modal isOpen={showCategoryVatModal} onClose={() => setShowCategoryVatModal(false)} title="Category VAT defaults" size="lg">
        <div className="overflow-x-auto">
          <table className="min-w-full text-xs">
            <thead className="bg-lime-100">
              <tr>
                <th className="px-3 py-2 text-left font-semibold text-gray-700">Category</th>
                <th className="px-3 py-2 text-right font-semibold text-gray-700">VAT Rate</th>
                <th className="px-3 py-2 text-left font-semibold text-gray-700">Tax Type</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-700">Claimable</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-700">Entertainment</th>
                <th className="px-3 py-2 text-center font-semibold text-gray-700">Lock</th>
                <th className="px-3 py-2"></th>
              </tr>
            </thead>
            <tbody className="divide-y divide-lime-200">
              {categories.map((cat) => {
                const edit = categoryVatEdits[cat.id] || {}
                return (
                  <tr key={cat.id} className="hover:bg-lime-50">
                    <td className="px-3 py-2 font-medium">{cat.name}</td>
                    <td className="px-3 py-2">
                      <input type="number" step="0.01" min="0" max="1" className="w-16 border border-lime-300 rounded px-2 py-1 text-right" value={edit.defaultVatRate ?? ''} onChange={(e) => setCategoryVatEdits((prev) => ({ ...prev, [cat.id]: { ...prev[cat.id], defaultVatRate: parseFloat(e.target.value) || 0 } }))} />
                    </td>
                    <td className="px-3 py-2">
                      <select className="w-full border border-lime-300 rounded px-2 py-1" value={edit.defaultTaxType ?? 'Standard'} onChange={(e) => setCategoryVatEdits((prev) => ({ ...prev, [cat.id]: { ...prev[cat.id], defaultTaxType: e.target.value } }))}>
                        <option value="Standard">Standard</option>
                        <option value="Exempt">Exempt</option>
                        <option value="OutOfScope">Out of Scope</option>
                        <option value="Petroleum">Petroleum</option>
                      </select>
                    </td>
                    <td className="px-3 py-2 text-center">
                      <input type="checkbox" checked={!!edit.defaultIsTaxClaimable} onChange={(e) => setCategoryVatEdits((prev) => ({ ...prev, [cat.id]: { ...prev[cat.id], defaultIsTaxClaimable: e.target.checked } }))} />
                    </td>
                    <td className="px-3 py-2 text-center">
                      <input type="checkbox" checked={!!edit.defaultIsEntertainment} onChange={(e) => setCategoryVatEdits((prev) => ({ ...prev, [cat.id]: { ...prev[cat.id], defaultIsEntertainment: e.target.checked } }))} />
                    </td>
                    <td className="px-3 py-2 text-center">
                      <input type="checkbox" checked={!!edit.vatDefaultLocked} onChange={(e) => setCategoryVatEdits((prev) => ({ ...prev, [cat.id]: { ...prev[cat.id], vatDefaultLocked: e.target.checked } }))} />
                    </td>
                    <td className="px-3 py-2">
                      <button type="button" disabled={updatingCategoryId === cat.id} className="px-2 py-1 text-xs bg-green-600 text-white rounded hover:bg-green-700 disabled:opacity-50" onClick={async () => {
                        setUpdatingCategoryId(cat.id)
                        try {
                          const res = await expensesAPI.updateCategory(cat.id, categoryVatEdits[cat.id])
                          if (res?.success) { toast.success('Category updated'); await fetchCategories() }
                          else toast.error(res?.message || 'Update failed')
                        } catch (e) { toast.error(e?.response?.data?.message || 'Update failed') }
                        finally { setUpdatingCategoryId(null) }
                      }}>
                        {updatingCategoryId === cat.id ? 'Saving...' : 'Update'}
                      </button>
                    </td>
                  </tr>
                )
              })}
            </tbody>
          </table>
        </div>
        <div className="mt-4 flex justify-end">
          <button type="button" onClick={() => setShowCategoryVatModal(false)} className="px-4 py-2 border-2 border-lime-300 rounded text-sm font-medium hover:bg-lime-50">Close</button>
        </div>
      </Modal>

      <BulkVatUpdateModal
        isOpen={showBulkVatModal}
        onClose={() => { setShowBulkVatModal(false); setSelectedExpenseIds([]) }}
        onSuccess={() => { setShowEditModal(false); setSelectedExpense(null); fetchExpenses(); setSelectedExpenseIds([]) }}
        scope={selectedExpenseIds.length > 0 ? 'selected' : 'all'}
        expenseIds={selectedExpenseIds}
        noVatCount={noVatCount}
        previewExpenses={filteredExpenses.filter((e) => selectedExpenseIds.includes(e.id))}
      />
    </div>
  )
}

export default ExpensesPage
