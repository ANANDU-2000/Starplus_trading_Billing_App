/*
Purpose: Purchase and PurchaseItem models for supplier purchases
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Purchase
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(200)]
        public string SupplierName { get; set; } = string.Empty;
        [Required]
        [MaxLength(100)]
        public string InvoiceNo { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? ExternalReference { get; set; } // For idempotency - unique external reference
        
        [MaxLength(100)]
        public string? ExpenseCategory { get; set; } // Track purchase expense type (e.g., "Inventory", "Supplies", "Equipment")
        
        public DateTime PurchaseDate { get; set; }
        public decimal TotalAmount { get; set; }
        public string? InvoiceFilePath { get; set; }
        public string? InvoiceFileName { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual ICollection<PurchaseItem> Items { get; set; } = new List<PurchaseItem>();
        public virtual User CreatedByUser { get; set; } = null!;
    }

    public class PurchaseItem
    {
        public int Id { get; set; }
        public int PurchaseId { get; set; }
        public int ProductId { get; set; }
        [Required]
        [MaxLength(20)]
        public string UnitType { get; set; } = "CRTN";
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; }
        public decimal LineTotal { get; set; }

        // Navigation properties
        public virtual Purchase Purchase { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }
}

