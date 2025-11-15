import { Plus, Receipt, UserPlus, FileText, Database, CheckCircle, XCircle, Clock } from 'lucide-react'
import { useNavigate } from 'react-router-dom'
import { useAuth } from '../hooks/useAuth'

const QuickActionsPanel = ({ onBackup, backupLoading, dbStatus, lastBackup, onCreateCustomer, onOpenLedger }) => {
  const navigate = useNavigate()
  const { user } = useAuth()
  const isAdmin = user?.role?.toLowerCase() === 'admin'

  return (
    <div className="bg-gradient-to-br from-blue-50 to-indigo-50 rounded-lg shadow-sm border-2 border-blue-200 p-6 h-full flex flex-col">
      <h3 className="text-lg font-semibold text-gray-900 mb-4">Quick Actions</h3>
      
      <div className="space-y-3 flex-1">
        {/* Create Bill */}
        <button
          onClick={() => navigate('/pos')}
          className="w-full flex items-center justify-between px-4 py-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow border border-gray-200 group"
        >
          <div className="flex items-center">
            <Plus className="h-5 w-5 text-blue-600 mr-3" />
            <span className="font-medium text-gray-900">Create Bill</span>
          </div>
          <span className="text-xs text-gray-500">Ctrl+B</span>
        </button>

        {/* Create Purchase */}
        <button
          onClick={() => navigate('/purchases')}
          className="w-full flex items-center justify-between px-4 py-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow border border-gray-200 group"
        >
          <div className="flex items-center">
            <Receipt className="h-5 w-5 text-green-600 mr-3" />
            <span className="font-medium text-gray-900">Create Purchase</span>
          </div>
          <span className="text-xs text-gray-500">Ctrl+U</span>
        </button>

        {/* Create Customer */}
        <button
          onClick={onCreateCustomer}
          className="w-full flex items-center justify-between px-4 py-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow border border-gray-200 group"
        >
          <div className="flex items-center">
            <UserPlus className="h-5 w-5 text-purple-600 mr-3" />
            <span className="font-medium text-gray-900">Create Customer</span>
          </div>
          <span className="text-xs text-gray-500">Ctrl+C</span>
        </button>

        {/* Ledger */}
        <button
          onClick={onOpenLedger}
          className="w-full flex items-center justify-between px-4 py-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow border border-gray-200 group"
        >
          <div className="flex items-center">
            <FileText className="h-5 w-5 text-orange-600 mr-3" />
            <span className="font-medium text-gray-900">Customer Ledger</span>
          </div>
          <span className="text-xs text-gray-500">Ctrl+L</span>
        </button>

        {/* Backup (Admin only) */}
        {isAdmin && (
          <button
            onClick={onBackup}
            disabled={backupLoading}
            className="w-full flex items-center justify-between px-4 py-3 bg-white rounded-lg shadow-sm hover:shadow-md transition-shadow border border-gray-200 group disabled:opacity-50"
          >
            <div className="flex items-center">
              <Database className="h-5 w-5 text-indigo-600 mr-3" />
              <span className="font-medium text-gray-900">Run Backup</span>
            </div>
            <span className="text-xs text-gray-500">Ctrl+K</span>
          </button>
        )}
      </div>

      {/* Status Footer */}
      <div className="mt-6 pt-4 border-t border-gray-300 space-y-2">
        {/* DB Status */}
        <div className="flex items-center justify-between text-sm">
          <span className="text-gray-600">DB Status:</span>
          <div className="flex items-center">
            {dbStatus ? (
              <>
                <CheckCircle className="h-4 w-4 text-green-500 mr-1" />
                <span className="text-green-600">Connected</span>
              </>
            ) : (
              <>
                <XCircle className="h-4 w-4 text-red-500 mr-1" />
                <span className="text-red-600">Disconnected</span>
              </>
            )}
          </div>
        </div>

        {/* Last Backup (Admin only) */}
        {isAdmin && (
          <div className="flex items-center justify-between text-sm">
            <span className="text-gray-600">Last Backup:</span>
            <div className="flex items-center text-gray-500">
              <Clock className="h-3 w-3 mr-1" />
              <span>{lastBackup ? new Date(lastBackup).toLocaleString() : 'Never'}</span>
            </div>
          </div>
        )}
      </div>
    </div>
  )
}

export default QuickActionsPanel

