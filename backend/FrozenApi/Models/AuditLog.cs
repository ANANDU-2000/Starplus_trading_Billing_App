/*
Purpose: AuditLog model for tracking user actions
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class AuditLog
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        [Required]
        [MaxLength(200)]
        public string Action { get; set; } = string.Empty;
        public string? Details { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
    }
}

