/*
Purpose: Setting model for application configuration
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Setting
    {
        [Key]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;
        public string? Value { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime UpdatedAt { get; set; }
    }
}

