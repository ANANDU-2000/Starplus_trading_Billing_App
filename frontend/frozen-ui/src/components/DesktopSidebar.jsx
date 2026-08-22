import { Link } from 'react-router-dom'
import { LogOut } from 'lucide-react'
import { getMainNavigation, isNavActive } from '../config/navigation'

const DesktopSidebar = ({ user, pathname, onLogout, activeHref }) => {
  const navigation = getMainNavigation(user)

  return (
    <div className="hidden lg:fixed lg:inset-y-0 lg:flex lg:w-20 lg:flex-col z-40">
      <div className="flex flex-col flex-grow bg-blue-800 text-white shadow-2xl">
        <nav className="flex-1 px-2 py-4 space-y-2 overflow-y-auto">
          {navigation.map((item) => {
            const Icon = item.icon
            const active = activeHref
              ? item.href === activeHref
              : isNavActive(pathname, item.href)
            return (
              <Link
                key={item.name}
                to={item.href}
                className={`flex flex-col items-center justify-center px-3 py-2 text-sm font-medium rounded-lg transition-colors ${
                  active ? 'bg-blue-600 text-white' : 'text-blue-100 hover:bg-blue-700'
                }`}
                title={item.name}
              >
                <Icon className="h-5 w-5 mb-1" />
                <span className="text-xs text-center leading-tight">{item.name}</span>
              </Link>
            )
          })}
        </nav>
        <div className="border-t border-blue-700 p-2">
          <button
            type="button"
            onClick={onLogout}
            className="flex flex-col items-center justify-center w-full px-3 py-2 text-sm text-blue-100 hover:text-white hover:bg-blue-700 rounded-lg transition-colors"
          >
            <LogOut className="h-5 w-5 mb-1" />
            <span className="text-xs">Logout</span>
          </button>
        </div>
      </div>
    </div>
  )
}

export default DesktopSidebar
