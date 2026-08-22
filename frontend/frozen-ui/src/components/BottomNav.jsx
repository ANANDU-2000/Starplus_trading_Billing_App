import { Link, useLocation } from 'react-router-dom'
import { bottomNavItems, isNavActive } from '../config/navigation'

const BottomNav = () => {
  const location = useLocation()

  return (
    <div className="fixed bottom-0 left-0 right-0 bg-white border-t border-gray-200 z-50 lg:hidden">
      <div className="flex justify-around items-center py-2">
        {bottomNavItems.map((item) => {
          const Icon = item.icon
          const active = isNavActive(location.pathname, item.href)
          return (
            <Link
              key={item.name}
              to={item.href}
              className={`flex flex-col items-center py-2 px-3 rounded-lg transition-colors ${
                active ? 'text-blue-600 bg-blue-50' : 'text-gray-500 hover:text-gray-700'
              }`}
            >
              <Icon className="h-5 w-5 mb-1" />
              <span className="text-xs font-medium">{item.name}</span>
            </Link>
          )
        })}
      </div>
    </div>
  )
}

export default BottomNav
