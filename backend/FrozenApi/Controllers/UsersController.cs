/*
Purpose: Users controller for Admin to manage users (Staff and Admin)
Author: AI Assistant
Date: 2024
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FrozenApi.Services;
using FrozenApi.Models;
using FrozenApi.Data;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly IAuthService _authService;
        private readonly AppDbContext _context;

        public UsersController(IAuthService authService, AppDbContext context)
        {
            _authService = authService;
            _context = context;
        }

        // GET: api/users - Get all users (Admin only)
        [HttpGet]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PagedResponse<UserDto>>>> GetUsers(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string? search = null,
            [FromQuery] string? role = null)
        {
            try
            {
                var query = _context.Users.AsQueryable();

                // Filter by role
                if (!string.IsNullOrEmpty(role) && Enum.TryParse<UserRole>(role, true, out var roleEnum))
                {
                    query = query.Where(u => u.Role == roleEnum);
                }

                // Search filter
                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(u => 
                        u.Name.Contains(search) || 
                        u.Email.Contains(search) ||
                        (u.Phone != null && u.Phone.Contains(search)));
                }

                var totalCount = await query.CountAsync();
                var users = await query
                    .OrderBy(u => u.Name)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        Role = u.Role.ToString(),
                        Phone = u.Phone,
                        CreatedAt = u.CreatedAt
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<PagedResponse<UserDto>>
                {
                    Success = true,
                    Message = "Users retrieved successfully",
                    Data = new PagedResponse<UserDto>
                    {
                        Items = users,
                        TotalCount = totalCount,
                        Page = page,
                        PageSize = pageSize,
                        TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                    }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PagedResponse<UserDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // GET: api/users/{id} - Get user by ID (Admin only)
        [HttpGet("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUser(int id)
        {
            try
            {
                var user = await _context.Users
                    .Where(u => u.Id == id)
                    .Select(u => new UserDto
                    {
                        Id = u.Id,
                        Name = u.Name,
                        Email = u.Email,
                        Role = u.Role.ToString(),
                        Phone = u.Phone,
                        CreatedAt = u.CreatedAt
                    })
                    .FirstOrDefaultAsync();

                if (user == null)
                {
                    return NotFound(new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                return Ok(new ApiResponse<UserDto>
                {
                    Success = true,
                    Message = "User retrieved successfully",
                    Data = user
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // POST: api/users - Create new user (Admin only)
        [HttpPost]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<RegisterResponse>>> CreateUser([FromBody] CreateUserRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int currentUserId))
                {
                    return Unauthorized(new ApiResponse<RegisterResponse>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                // Validate role
                if (!Enum.TryParse<UserRole>(request.Role, true, out var role))
                {
                    return BadRequest(new ApiResponse<RegisterResponse>
                    {
                        Success = false,
                        Message = "Invalid role. Must be 'Admin' or 'Staff'"
                    });
                }

                // Convert to RegisterRequest
                var registerRequest = new RegisterRequest
                {
                    Name = request.Name,
                    Email = request.Email,
                    Password = request.Password,
                    Role = request.Role,
                    Phone = request.Phone
                };

                var result = await _authService.RegisterAsync(registerRequest, currentUserId);

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
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<RegisterResponse>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // PUT: api/users/{id} - Update user (Admin only)
        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<UserDto>>> UpdateUser(int id, [FromBody] UpdateUserRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<UserDto>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Update fields
                if (!string.IsNullOrEmpty(request.Name))
                {
                    user.Name = request.Name.Trim();
                }

                if (!string.IsNullOrEmpty(request.Phone))
                {
                    user.Phone = request.Phone.Trim();
                }

                // Update role if provided
                if (!string.IsNullOrEmpty(request.Role))
                {
                    if (Enum.TryParse<UserRole>(request.Role, true, out var newRole))
                    {
                        user.Role = newRole;
                    }
                    else
                    {
                        return BadRequest(new ApiResponse<UserDto>
                        {
                            Success = false,
                            Message = "Invalid role. Must be 'Admin' or 'Staff'"
                        });
                    }
                }

                await _context.SaveChangesAsync();

                // Create audit log
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int currentUserId))
                {
                    var auditLog = new AuditLog
                    {
                        UserId = currentUserId,
                        Action = "User Updated",
                        Details = $"Updated user: {user.Email}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                }

                var userDto = new UserDto
                {
                    Id = user.Id,
                    Name = user.Name,
                    Email = user.Email,
                    Role = user.Role.ToString(),
                    Phone = user.Phone,
                    CreatedAt = user.CreatedAt
                };

                return Ok(new ApiResponse<UserDto>
                {
                    Success = true,
                    Message = "User updated successfully",
                    Data = userDto
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<UserDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // PUT: api/users/{id}/reset-password - Reset user password (Admin only)
        [HttpPut("{id}/reset-password")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> ResetPassword(int id, [FromBody] ResetPasswordRequest request)
        {
            try
            {
                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Hash new password
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.NewPassword);
                await _context.SaveChangesAsync();

                // Create audit log
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim != null && int.TryParse(userIdClaim.Value, out int currentUserId))
                {
                    var auditLog = new AuditLog
                    {
                        UserId = currentUserId,
                        Action = "Password Reset",
                        Details = $"Password reset for user: {user.Email}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                }

                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Message = "Password reset successfully",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<bool>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        // DELETE: api/users/{id} - Delete user (Admin only)
        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int currentUserId))
                {
                    return Unauthorized(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                // Prevent deleting yourself
                if (id == currentUserId)
                {
                    return BadRequest(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "You cannot delete your own account"
                    });
                }

                var user = await _context.Users.FindAsync(id);
                if (user == null)
                {
                    return NotFound(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "User not found"
                    });
                }

                // Check if user has any associated records (sales, etc.)
                var hasSales = await _context.Sales.AnyAsync(s => s.CreatedBy == id || s.LastModifiedBy == id);
                if (hasSales)
                {
                    return BadRequest(new ApiResponse<bool>
                    {
                        Success = false,
                        Message = "Cannot delete user with associated transactions. Consider deactivating instead."
                    });
                }

                _context.Users.Remove(user);
                await _context.SaveChangesAsync();

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = currentUserId,
                    Action = "User Deleted",
                    Details = $"Deleted user: {user.Email}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                return Ok(new ApiResponse<bool>
                {
                    Success = true,
                    Message = "User deleted successfully",
                    Data = true
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<bool>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

