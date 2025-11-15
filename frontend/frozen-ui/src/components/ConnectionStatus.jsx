import React, { useState, useEffect } from 'react'
import { connectionManager } from '../services/connectionManager'
import { Wifi, WifiOff } from 'lucide-react'

function ConnectionStatus() {
  const [isConnected, setIsConnected] = useState(true)
  const [isVisible, setIsVisible] = useState(false)

  useEffect(() => {
    // Subscribe to connection status changes
    const unsubscribe = connectionManager.onStatusChange((connected) => {
      setIsConnected(connected)
      setIsVisible(!connected) // Show when disconnected
      
      // Auto-hide after 5 seconds if connected
      if (connected) {
        setTimeout(() => {
          setIsVisible(false)
        }, 5000)
      }
    })

    // Initial check
    setIsConnected(connectionManager.isConnected)

    return () => {
      if (unsubscribe) {
        unsubscribe()
      }
    }
  }, [])

  if (!isVisible) {
    return null
  }

  return (
    <div className={`fixed top-4 right-4 z-50 flex items-center gap-2 px-4 py-2 rounded-lg shadow-lg transition-all ${
      isConnected 
        ? 'bg-green-500 text-white' 
        : 'bg-red-500 text-white animate-pulse'
    }`}>
      {isConnected ? (
        <>
          <Wifi className="w-4 h-4" />
          <span className="text-sm font-medium">Connected</span>
        </>
      ) : (
        <>
          <WifiOff className="w-4 h-4" />
          <span className="text-sm font-medium">Server Disconnected</span>
        </>
      )}
    </div>
  )
}

export default ConnectionStatus

