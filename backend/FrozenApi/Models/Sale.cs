/*
Purpose: Sale and SaleItem models for billing/POS
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Sale
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(100)]
        public string InvoiceNo { get; set; } = string.Empty;
        
        [MaxLength(200)]
        public string? ExternalReference { get; set; } // For idempotency - unique external reference (e.g., POS terminal ID, mobile app transaction ID)
        
        public DateTime InvoiceDate { get; set; }
        public int? CustomerId { get; set; }
        public decimal Subtotal { get; set; }
        public decimal VatTotal { get; set; }
        public decimal Discount { get; set; }
        public decimal GrandTotal { get; set; }
        public decimal TotalAmount { get; set; } // Alias for GrandTotal, kept for consistency
        public decimal PaidAmount { get; set; } = 0; // Total amount paid so far
        public SalePaymentStatus PaymentStatus { get; set; } // Pending, Partial, Paid
        public DateTime? LastPaymentDate { get; set; } // Last payment date
        public string? Notes { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public int? LastModifiedBy { get; set; } // Track who edited
        public DateTime? LastModifiedAt { get; set; } // When edited
        public bool IsDeleted { get; set; } = false; // Soft delete
        public int? DeletedBy { get; set; } // Who deleted
        public DateTime? DeletedAt { get; set; } // When deleted
        
        // Invoice finalization - stock is only decremented when IsFinalized = true
        public bool IsFinalized { get; set; } = true; // True when invoice is finalized and stock is decremented
        
        // 48-hour edit window fields
        public bool IsLocked { get; set; } = false; // Locked after 48 hours
        public DateTime? LockedAt { get; set; } // When locked
        public string? EditReason { get; set; } // Reason for edit (required for Staff)
        public int Version { get; set; } = 1; // Version number for tracking edits (also used for concurrency)
        
        // Concurrency control - prevent duplicate saves when multiple users edit simultaneously
        public byte[] RowVersion { get; set; } = Array.Empty<byte>(); // For optimistic concurrency

        // Navigation properties
        public virtual Customer? Customer { get; set; }
        public virtual User CreatedByUser { get; set; } = null!;
        public virtual User? LastModifiedByUser { get; set; }
        public virtual User? DeletedByUser { get; set; }
        public virtual ICollection<SaleItem> Items { get; set; } = new List<SaleItem>();
    }

    public class SaleItem
    {
        public int Id { get; set; }
        public int SaleId { get; set; }
        public int ProductId { get; set; }
        [Required]
        [MaxLength(20)]
        public string UnitType { get; set; } = "CRTN";
        public decimal Qty { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal Discount { get; set; }
        public decimal VatAmount { get; set; }
        public decimal LineTotal { get; set; }

        // Navigation properties
        public virtual Sale Sale { get; set; } = null!;
        public virtual Product Product { get; set; } = null!;
    }

    public enum SalePaymentStatus
    {
        Pending,
        Partial,
        Paid
    }
}

