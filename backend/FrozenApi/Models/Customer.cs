/*
Purpose: Customer model for customer management
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Customer
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(20)]
        public string? Phone { get; set; }
        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }
        [MaxLength(50)]
        public string? Trn { get; set; }
        [MaxLength(500)]
        public string? Address { get; set; }
        public decimal CreditLimit { get; set; }
            
        // REAL-TIME BALANCE TRACKING FIELDS
        public decimal TotalSales { get; set; } = 0; // Sum of all invoice GrandTotal (excluding deleted)
        public decimal TotalPayments { get; set; } = 0; // Sum of all CLEARED payments
        public decimal PendingBalance { get; set; } = 0; // TotalSales - TotalPayments (amount customer owes)
            
        // Legacy balance field (kept for backward compatibility, but use PendingBalance instead)
        public decimal Balance { get; set; } // Positive = customer owes, negative = customer has credit
            
        public DateTime? LastActivity { get; set; } // Last transaction date
        public DateTime? LastPaymentDate { get; set; } // Last payment received date
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // For optimistic concurrency
    }
}

