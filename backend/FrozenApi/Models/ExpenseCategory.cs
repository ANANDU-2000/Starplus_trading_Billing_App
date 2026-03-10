/*
Purpose: Expense Category model for categorizing expenses
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class ExpenseCategory
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string Name { get; set; } = string.Empty;
        [MaxLength(7)]
        public string ColorCode { get; set; } = "#3B82F6"; // Default blue
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public decimal DefaultVatRate { get; set; } = 0m;
        [MaxLength(20)]
        public string DefaultTaxType { get; set; } = "Standard";
        public bool DefaultIsTaxClaimable { get; set; } = false;
        public bool DefaultIsEntertainment { get; set; } = false;
        public bool VatDefaultLocked { get; set; } = false;
        
        // Navigation properties
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
    }
}

