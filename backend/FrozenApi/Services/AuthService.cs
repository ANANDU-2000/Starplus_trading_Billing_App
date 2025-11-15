/*
Purpose: Authentication service for user login and JWT token management
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IAuthService
    {
        Task<LoginResponse?> LoginAsync(LoginRequest request);
        Task<RegisterResponse> RegisterAsync(RegisterRequest request, int createdByUserId);
        Task<bool> ValidateTokenAsync(string token);
        Task<User?> GetUserByIdAsync(int userId);
    }

    public class AuthService : IAuthService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;

        public AuthService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<LoginResponse?> LoginAsync(LoginRequest request)
        {
            // Normalize email to lowercase for case-insensitive comparison
            var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            
            if (string.IsNullOrEmpty(normalizedEmail))
            {
                return null;
            }

            // Use case-insensitive comparison with EF.Functions for better database performance
            // For SQLite, we'll use Contains with case-insensitive collation or fallback to in-memory
            var allUsers = await _context.Users.ToListAsync();
            var user = allUsers.FirstOrDefault(u => 
                (u.Email?.Trim().ToLowerInvariant() ?? string.Empty) == normalizedEmail);

            if (user == null)
            {
                return null;
            }

            // Verify password - with better error handling
            try
            {
                if (string.IsNullOrEmpty(user.PasswordHash))
                {
                    return null;
                }

                if (!BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash))
                {
                    return null;
                }
            }
            catch (Exception ex)
            {
                // Log password verification errors for debugging
                // Note: In production, you might want to use ILogger here
                System.Diagnostics.Debug.WriteLine($"Password verification error for {normalizedEmail}: {ex.Message}");
                return null;
            }

            // Determine token expiry: 30 days if RememberMe, otherwise 8 hours
            var expiryHours = request.RememberMe ? 24 * 30 : 8;
            var token = GenerateJwtToken(user, expiryHours);
            var companyName = await GetCompanyNameAsync();

            return new LoginResponse
            {
                Token = token,
                Role = user.Role.ToString(),
                UserId = user.Id,
                Name = user.Name,
                CompanyName = companyName,
                ExpiresAt = DateTime.UtcNow.AddHours(expiryHours)
            };
        }

        public async Task<RegisterResponse> RegisterAsync(RegisterRequest request, int createdByUserId)
        {
            // Normalize email
            var normalizedEmail = request.Email?.Trim().ToLowerInvariant() ?? string.Empty;
            
            if (string.IsNullOrEmpty(normalizedEmail))
            {
                throw new InvalidOperationException("Email is required");
            }

            // Check if email already exists
            var allUsers = await _context.Users.ToListAsync();
            var existingUser = allUsers.FirstOrDefault(u => 
                (u.Email?.Trim().ToLowerInvariant() ?? string.Empty) == normalizedEmail);

            if (existingUser != null)
            {
                throw new InvalidOperationException("Email already exists");
            }

            // Validate role
            if (!Enum.TryParse<UserRole>(request.Role, out var role))
            {
                role = UserRole.Staff; // Default to Staff if invalid
            }

            // Hash password
            var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);

            // Create new user
            var user = new User
            {
                Name = request.Name.Trim(),
                Email = normalizedEmail,
                PasswordHash = passwordHash,
                Role = role,
                Phone = request.Phone?.Trim(),
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            // Log action in audit log if AuditLogs table exists
            try
            {
                var creator = await _context.Users.FindAsync(createdByUserId);
                if (creator != null)
                {
                    var auditLog = new AuditLog
                    {
                        UserId = createdByUserId,
                        Action = "User Created",
                        Details = $"Created user: {user.Email} with role {user.Role}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AuditLogs.Add(auditLog);
                    await _context.SaveChangesAsync();
                }
            }
            catch
            {
                // Audit logging is optional, continue if it fails
            }

            return new RegisterResponse
            {
                UserId = user.Id,
                Email = user.Email,
                Name = user.Name,
                Role = user.Role.ToString(),
                Message = "User created successfully"
            };
        }

        public Task<bool> ValidateTokenAsync(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!);
                
                tokenHandler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                }, out SecurityToken validatedToken);

                return Task.FromResult(true);
            }
            catch
            {
                return Task.FromResult(false);
            }
        }

        public async Task<User?> GetUserByIdAsync(int userId)
        {
            return await _context.Users.FindAsync(userId);
        }

        private string GenerateJwtToken(User user, int? customExpiryHours = null)
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.UTF8.GetBytes(_configuration["JwtSettings:SecretKey"]!);
            var jwtSettings = _configuration.GetSection("JwtSettings");
            var issuer = jwtSettings["Issuer"] ?? "FrozenApi";
            var audience = jwtSettings["Audience"] ?? "FrozenApi";
            var expiryHours = customExpiryHours ?? (int.TryParse(jwtSettings["ExpiryInHours"], out int hours) ? hours : 8);
            
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[]
                {
                    new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                    new Claim(ClaimTypes.Email, user.Email),
                    new Claim(ClaimTypes.Role, user.Role.ToString()),
                    new Claim(ClaimTypes.Name, user.Name)
                }),
                Issuer = issuer,
                Audience = audience,
                Expires = DateTime.UtcNow.AddHours(expiryHours),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        private async Task<string> GetCompanyNameAsync()
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "COMPANY_NAME_EN");
            
            return setting?.Value ?? "Starplus Foodstuff Trading";
        }
    }
}

