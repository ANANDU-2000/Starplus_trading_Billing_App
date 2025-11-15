import { TrendingUp, TrendingDown } from 'lucide-react'

const StatCard = ({ 
  title, 
  value, 
  change, 
  changeType = 'neutral', 
  icon: Icon, 
  iconColor = 'blue',
  format = 'currency' 
}) => {
  const formatValue = (val) => {
    if (format === 'currency') {
      return new Intl.NumberFormat('en-AE', {
        style: 'currency',
        currency: 'AED',
        minimumFractionDigits: 0,
        maximumFractionDigits: 0
      }).format(val)
    }
    return val
  }

  const iconColors = {
    blue: 'bg-blue-100 text-blue-600',
    green: 'bg-green-100 text-green-600',
    orange: 'bg-orange-100 text-orange-600',
    purple: 'bg-purple-100 text-purple-600',
    red: 'bg-red-100 text-red-600'
  }

  return (
    <div className="bg-white rounded-lg sm:rounded-xl shadow-sm border border-gray-200 p-2 sm:p-4 lg:p-6 hover:shadow-md transition-shadow duration-200">
      <div className="flex items-center justify-between">
        <div className="flex-1 min-w-0">
          <p className="text-xs sm:text-sm font-medium text-gray-600 mb-0.5 sm:mb-1 truncate">{title}</p>
          <p className="text-base sm:text-xl lg:text-2xl font-bold text-gray-900 truncate">{formatValue(value)}</p>
          {change !== undefined && (
            <div className="flex items-center mt-1 sm:mt-2">
              {changeType === 'positive' ? (
                <TrendingUp className="h-3 w-3 sm:h-4 sm:w-4 text-green-600 mr-0.5 sm:mr-1 flex-shrink-0" />
              ) : changeType === 'negative' ? (
                <TrendingDown className="h-3 w-3 sm:h-4 sm:w-4 text-red-600 mr-0.5 sm:mr-1 flex-shrink-0" />
              ) : null}
              <span className={`text-xs sm:text-sm font-medium ${
                changeType === 'positive' ? 'text-green-600' : 
                changeType === 'negative' ? 'text-red-600' : 
                'text-gray-600'
              }`}>
                {change !== 0 && (changeType === 'positive' ? '+' : '')}
                {change}% {changeType === 'neutral' ? '' : changeType === 'positive' ? '↑' : '↓'}
              </span>
            </div>
          )}
        </div>
        {Icon && (
          <div className={`p-1.5 sm:p-2 lg:p-3 rounded-lg ${iconColors[iconColor]} flex-shrink-0 ml-2`}>
            <Icon className="h-4 w-4 sm:h-5 sm:w-5 lg:h-6 lg:w-6" />
          </div>
        )}
      </div>
    </div>
  )
}

export default StatCard

