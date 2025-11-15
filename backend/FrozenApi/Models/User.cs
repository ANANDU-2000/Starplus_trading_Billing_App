/*
Purpose: User model for authentication and authorization
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class User
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;
        [Required]
        public string PasswordHash { get; set; } = string.Empty;
        public UserRole Role { get; set; }
        [MaxLength(20)]
        public string? Phone { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public enum UserRole
    {
        Admin,
        Staff
    }
}

