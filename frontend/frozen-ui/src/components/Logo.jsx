import React from 'react'

const Logo = ({ className = '', showText = true, size = 'default' }) => {
  const sizeClasses = {
    small: 'h-8 w-8',
    default: 'h-10 w-10',
    large: 'h-16 w-16',
    xl: 'h-24 w-24'
  }

  const textSizeClasses = {
    small: 'text-sm',
    default: 'text-base',
    large: 'text-xl',
    xl: 'text-2xl'
  }

  return (
    <div className={`flex items-center space-x-2 ${className}`}>
      <img 
        src="/starplus-logo.svg" 
        alt="Starplus Foodstuff Trading" 
        className={`${sizeClasses[size]} object-contain`}
      />
      {showText && (
        <div className={`font-bold text-blue-800 ${textSizeClasses[size]} hidden sm:block`}>
          <span className="text-orange-500">Star</span>
          <span className="text-blue-800">plus</span>
        </div>
      )}
    </div>
  )
}

export default Logo

