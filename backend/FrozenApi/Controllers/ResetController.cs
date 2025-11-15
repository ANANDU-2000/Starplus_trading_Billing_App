/*
Purpose: Reset controller for admin system reset functionality
Author: AI Assistant
Date: 2025
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    // PRODUCTION SAFETY: This controller is disabled in production to prevent accidental data loss
    // Only enable in Development/Staging environments
    #if !DEBUG
    [ApiExplorerSettings(IgnoreApi = true)] // Hide from Swagger in production
    #endif
    public class ResetController : ControllerBase
    {
        private readonly IResetService _resetService;

        public ResetController(IResetService resetService)
        {
            _resetService = resetService;
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<SystemSummary>>> GetSystemSummary()
        {
            try
            {
                var summary = await _resetService.GetSystemSummaryAsync();
                return Ok(new ApiResponse<SystemSummary>
                {
                    Success = true,
                    Message = "System summary retrieved successfully",
                    Data = summary
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<SystemSummary>
                {
                    Success = false,
                    Message = "Failed to retrieve system summary",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("execute")]
        public async Task<ActionResult<ApiResponse<ResetResult>>> ExecuteReset(
            [FromBody] ResetRequest request)
        {
            // PRODUCTION SAFETY: Disable reset in production
            var environment = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT");
            if (environment == "Production")
            {
                return BadRequest(new ApiResponse<ResetResult>
                {
                    Success = false,
                    Message = "System reset is disabled in production for safety. Use migration scripts instead."
                });
            }

            try
            {
                // Get user ID from JWT token - try multiple claim types
                var userIdClaim = User.FindFirst("UserId") ?? 
                                  User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? 
                                  User.FindFirst("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<ResetResult>
                    {
                        Success = false,
                        Message = "Invalid user authentication"
                    });
                }

                // Validate confirmation text
                if (string.IsNullOrWhiteSpace(request.ConfirmationText) || 
                    request.ConfirmationText.Trim().ToUpper() != "RESET STARPLUS")
                {
                    return BadRequest(new ApiResponse<ResetResult>
                    {
                        Success = false,
                        Message = "Confirmation text must be exactly 'RESET STARPLUS'"
                    });
                }

                var result = await _resetService.ResetSystemAsync(
                    request.CreateBackup,
                    request.ClearAuditLogs,
                    userId
                );

                if (result.Success)
                {
                    return Ok(new ApiResponse<ResetResult>
                    {
                        Success = true,
                        Message = result.Message,
                        Data = result
                    });
                }
                else
                {
                    return BadRequest(new ApiResponse<ResetResult>
                    {
                        Success = false,
                        Message = result.Message,
                        Data = result
                    });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ResetResult>
                {
                    Success = false,
                    Message = "Reset operation failed",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }

    public class ResetRequest
    {
        public bool CreateBackup { get; set; }
        public bool ClearAuditLogs { get; set; }
        public string ConfirmationText { get; set; } = string.Empty;
    }
}

