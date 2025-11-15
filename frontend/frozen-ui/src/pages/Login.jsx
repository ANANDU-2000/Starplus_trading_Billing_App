import { useState, useEffect, useRef } from 'react'
import { useNavigate } from 'react-router-dom'
import { useForm } from 'react-hook-form'
import { Eye, EyeOff, Lock, Mail } from 'lucide-react'
import { useAuth } from '../hooks/useAuth'
import { Input } from '../components/Form'
import { LoadingButton } from '../components/Loading'
import { showToast } from '../utils/toast'
import Logo from '../components/Logo'

const Login = () => {
  const [showPassword, setShowPassword] = useState(false)
  const [loading, setLoading] = useState(false)
  const [rememberMe, setRememberMe] = useState(false)
  const { login } = useAuth()
  const navigate = useNavigate()
  const emailInputRef = useRef(null)

  const {
    register,
    handleSubmit,
    formState: { errors }
  } = useForm()

  // Keyboard shortcuts: Ctrl+L to focus login, Enter to submit
  useEffect(() => {
    const handleKeyDown = (e) => {
      if (e.ctrlKey && e.key === 'l') {
        e.preventDefault()
        emailInputRef.current?.focus()
      }
    }

    document.addEventListener('keydown', handleKeyDown)
    return () => document.removeEventListener('keydown', handleKeyDown)
  }, [])

  const onSubmit = async (data) => {
    setLoading(true)
    try {
      const result = await login({ 
        email: data.email, 
        password: data.password,
        rememberMe: rememberMe
      })
      if (result?.success) {
        showToast.success('Login successful!')
        const role = result.data?.role?.toLowerCase() || 'staff'
        navigate(role === 'admin' ? '/dashboard' : '/pos')
      } else {
        // Check for specific error codes
        if (result?.status === 429) {
          showToast.error('Too many attempts. Please try again later.')
        } else if (result?.status === 401) {
          showToast.error('Email or password incorrect.')
        } else if (result?.status === 500) {
          showToast.error('Server error — try again later.')
        } else {
          showToast.error(result?.message || 'Login failed')
        }
      }
    } catch (error) {
      if (error.response?.status === 429) {
        showToast.error('Too many attempts. Please try again later.')
      } else if (error.response?.status === 401) {
        showToast.error('Email or password incorrect.')
      } else if (error.response?.status === 500) {
        showToast.error('Server error — try again later.')
      } else {
        showToast.error(error.message || 'Login failed')
      }
    } finally {
      setLoading(false)
    }
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 to-indigo-100 flex items-center justify-center py-12 px-4 sm:px-6 lg:px-8">
      <div className="max-w-md w-full space-y-8">
        {/* Header */}
        <div className="text-center">
          <div className="mx-auto flex justify-center mb-4">
            <Logo size="large" showText={true} />
          </div>
          <h2 className="mt-2 text-2xl font-bold text-gray-900">
            STARPLUS FOODSTUFF TRADING
          </h2>
          <p className="mt-2 text-sm text-gray-600">
            Sign in to your account
          </p>
        </div>

        {/* Form */}
        <div className="bg-white py-8 px-6 shadow-xl rounded-lg">
          <form className="space-y-6" onSubmit={handleSubmit(onSubmit)}>
            <Input
              ref={emailInputRef}
              label="Email Address"
              type="email"
              placeholder="Enter your email address"
              required
              error={errors.email?.message}
              {...register('email', {
                required: 'Email is required',
                pattern: {
                  value: /^[A-Z0-9._%+-]+@[A-Z0-9.-]+\.[A-Z]{2,}$/i,
                  message: 'Invalid email address'
                }
              })}
              icon={<Mail className="h-5 w-5 text-gray-400" />}
            />

            <div className="relative">
              <Input
                label="Password"
                type={showPassword ? 'text' : 'password'}
                placeholder="Enter your password"
                required
                error={errors.password?.message}
                {...register('password', {
                  required: 'Password is required',
                  minLength: {
                    value: 6,
                    message: 'Password must be at least 6 characters'
                  }
                })}
                icon={<Lock className="h-5 w-5 text-gray-400" />}
              />
              <button
                type="button"
                className="absolute right-3 top-8 text-gray-400 hover:text-gray-600"
                onClick={() => setShowPassword(!showPassword)}
              >
                {showPassword ? (
                  <EyeOff className="h-5 w-5" />
                ) : (
                  <Eye className="h-5 w-5" />
                )}
              </button>
            </div>

            <div className="flex items-center justify-between">
              <div className="flex items-center">
                <input
                  id="remember-me"
                  name="remember-me"
                  type="checkbox"
                  checked={rememberMe}
                  onChange={(e) => setRememberMe(e.target.checked)}
                  className="h-4 w-4 text-blue-600 focus:ring-blue-500 border-gray-300 rounded"
                />
                <label htmlFor="remember-me" className="ml-2 block text-sm text-gray-900">
                  Remember me
                </label>
              </div>

              <div className="text-sm">
                <a href="#" className="font-medium text-blue-600 hover:text-blue-500">
                  Forgot your password?
                </a>
              </div>
            </div>

            <LoadingButton
              type="submit"
              loading={loading}
              disabled={loading}
              className="w-full"
            >
              Sign in
            </LoadingButton>
          </form>
        </div>

        {/* Footer */}
        <div className="text-center text-sm text-gray-500">
          <p>© 2024 STAR PLUS FOODSTUFF TRADING. All rights reserved.</p>
        </div>
      </div>
    </div>
  )
}

export default Login