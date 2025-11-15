/*
Purpose: Admin alert model for system notifications
Author: AI Assistant
Date: 2025
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Alert
    {
        public int Id { get; set; }
        
        [Required]
        [MaxLength(100)]
        public string Type { get; set; } = string.Empty; // BackupFailed, BackupSuccess, DBMismatch, LowStock, OverdueInvoice, DuplicateInvoice, PaymentFailed, HostingBillingFailure, HighErrorRate
        
        [Required]
        [MaxLength(200)]
        public string Title { get; set; } = string.Empty;
        
        [MaxLength(2000)]
        public string? Message { get; set; }
        
        [MaxLength(50)]
        public string Severity { get; set; } = "Info"; // Info, Warning, Error, Critical
        
        public bool IsRead { get; set; } = false;
        
        public bool IsResolved { get; set; } = false;
        
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        
        public DateTime? ReadAt { get; set; }
        
        public DateTime? ResolvedAt { get; set; }
        
        public int? ResolvedBy { get; set; }
        
        [MaxLength(500)]
        public string? Metadata { get; set; } // JSON string for additional data
        
        // Navigation properties
        public virtual User? ResolvedByUser { get; set; }
    }

    public enum AlertType
    {
        BackupFailed,
        BackupSuccess,
        DBMismatch,
        LowStock,
        OverdueInvoice,
        DuplicateInvoice,
        DuplicatePayment,
        PaymentFailed,
        HostingBillingFailure,
        HighErrorRate,
        StockMismatch,
        InvoiceEditConflict,
        DuplicateEntry,
        InvoiceDeleted,
        PaymentCreated,
        PaymentDeleted,
        BalanceMismatch,
        StockNegative,
        DatabaseError,
        ValidationError
    }

    public enum AlertSeverity
    {
        Info,
        Warning,
        Error,
        Critical
    }
}

