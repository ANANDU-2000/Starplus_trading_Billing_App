/*
Purpose: Expense model for expense tracking
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public int CategoryId { get; set; }
        public decimal Amount { get; set; }
        public DateTime Date { get; set; }
        [MaxLength(500)]
        public string? Note { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        
        // Navigation properties
        public virtual ExpenseCategory Category { get; set; } = null!;
        public virtual User CreatedByUser { get; set; } = null!;
    }
}

