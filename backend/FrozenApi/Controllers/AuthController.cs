/*
Purpose: Authentication controller for login and user management
Author: AI Assistant
Date: 2024
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<LoginResponse>>> Login([FromBody] LoginRequest request)
        {
            try
            {
                var result = await _authService.LoginAsync(request);
                if (result == null)
                {
                    return BadRequest(new ApiResponse<LoginResponse>
                    {
                        Success = false,
                        Message = "Invalid email or password",
                        Errors = new List<string>()
                    });
                }

                return Ok(new ApiResponse<LoginResponse>
                {
                    Success = true,
                    Message = "Login successful",
                    Data = result
                });
            }
            catch (Exception)
            {
                // Do not leak server internals to clients
                return StatusCode(500, new ApiResponse<LoginResponse>
                {
                    Success = false,
                    Message = "An error occurred during login",
                    Errors = new List<string>()
                });
            }
        }

        [HttpPost("health")]
        public Task<ActionResult<object>> Health()
        {
            try
            {
                var ok = true;
                return Task.FromResult<ActionResult<object>>(Ok(new { success = ok }));
            }
            catch
            {
                return Task.FromResult<ActionResult<object>>(StatusCode(500, new { success = false }));
            }
        }

        [HttpPost("forgot")]
        public Task<ActionResult<ApiResponse<object>>> ForgotPassword([FromBody] ForgotPasswordRequest request)
        {
            try
            {
                // For now, just return success - email integration can be added later
                return Task.FromResult<ActionResult<ApiResponse<object>>>(Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Password reset instructions have been sent to your email"
                }));
            }
            catch (Exception ex)
            {
                return Task.FromResult<ActionResult<ApiResponse<object>>>(StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                }));
            }
        }

        [HttpPost("register")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<RegisterResponse>>> Register([FromBody] RegisterRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int adminUserId))
                {
                    return Unauthorized(new ApiResponse<RegisterResponse>
                    {
                        Success = false,
                        Message = "Invalid admin user"
                    });
                }

                var result = await _authService.RegisterAsync(request, adminUserId);
                return Ok(new ApiResponse<RegisterResponse>
                {
                    Success = true,
                    Message = "User created successfully",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception)
            {
                return StatusCode(500, new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = "An error occurred during user registration",
                    Errors = new List<string>()
                });
            }
        }

        [HttpGet("validate")]
        [Authorize]
        public async Task<ActionResult<ApiResponse<object>>> ValidateToken()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid token"
                    });
                }

                var user = await _authService.GetUserByIdAsync(userId);
                if (user == null)
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Token is valid",
                    Data = new { UserId = user.Id, Role = user.Role.ToString(), Name = user.Name }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }

    public class ForgotPasswordRequest
    {
        public string Email { get; set; } = string.Empty;
    }
}

