/*
Purpose: InventoryTransaction model for stock tracking
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class InventoryTransaction
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public decimal ChangeQty { get; set; }
        public TransactionType TransactionType { get; set; }
        public int? RefId { get; set; }
        public string? Reason { get; set; }
        public DateTime CreatedAt { get; set; }

        // Navigation properties
        public virtual Product Product { get; set; } = null!;
    }

    public enum TransactionType
    {
        Purchase,
        Sale,
        Adjustment,
        Return,
        PurchaseReturn
    }
}

