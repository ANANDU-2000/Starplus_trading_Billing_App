import { useState, useEffect } from 'react'
import { X, Maximize2, Minimize2 } from 'lucide-react'

const Modal = ({ 
  isOpen, 
  onClose, 
  title, 
  children, 
  size = 'md',
  showCloseButton = true,
  closeOnOverlayClick = true,
  allowFullscreen = false
}) => {
  const [isFullscreen, setIsFullscreen] = useState(false)
  useEffect(() => {
    const handleEscape = (e) => {
      if (e.key === 'Escape') {
        onClose()
      }
    }

    if (isOpen) {
      document.addEventListener('keydown', handleEscape)
      document.body.style.overflow = 'hidden'
    }

    return () => {
      document.removeEventListener('keydown', handleEscape)
      document.body.style.overflow = 'unset'
    }
  }, [isOpen, onClose])

  if (!isOpen) return null

  const sizeClasses = {
    sm: 'max-w-md',
    md: 'max-w-lg',
    lg: 'max-w-2xl',
    xl: 'max-w-4xl',
    full: 'max-w-7xl'
  }

  return (
    <div className={`fixed inset-0 z-50 ${isFullscreen ? 'overflow-hidden' : 'overflow-y-auto'}`}>
      <div className={`flex ${isFullscreen ? 'h-full' : 'min-h-screen items-center justify-center p-4'}`}>
        {/* Overlay */}
        <div 
          className="fixed inset-0 bg-black bg-opacity-50 transition-opacity"
          onClick={closeOnOverlayClick ? onClose : undefined}
        />
        
        {/* Modal */}
        <div className={`relative bg-white rounded-lg shadow-xl w-full ${isFullscreen ? 'max-w-full h-full m-0' : sizeClasses[size]} transform transition-all ${isFullscreen ? 'rounded-none' : ''}`}>
          {/* Header */}
          {(title || showCloseButton || allowFullscreen) && (
            <div className="flex items-center justify-between p-6 border-b border-gray-200">
              {title && (
                <h3 className="text-lg font-semibold text-gray-900">
                  {title}
                </h3>
              )}
              <div className="flex items-center gap-2">
                {allowFullscreen && (
                  <button
                    onClick={() => setIsFullscreen(!isFullscreen)}
                    className="text-gray-400 hover:text-gray-600 transition-colors"
                    title={isFullscreen ? 'Exit Fullscreen' : 'Fullscreen'}
                  >
                    {isFullscreen ? <Minimize2 className="h-5 w-5" /> : <Maximize2 className="h-5 w-5" />}
                  </button>
                )}
                {showCloseButton && (
                  <button
                    onClick={onClose}
                    className="text-gray-400 hover:text-gray-600 transition-colors"
                  >
                    <X className="h-6 w-6" />
                  </button>
                )}
              </div>
            </div>
          )}
          
          {/* Content */}
          <div className={`p-6 ${isFullscreen ? 'overflow-auto h-full' : ''}`}>
            {children}
          </div>
        </div>
      </div>
    </div>
  )
}

export default Modal
