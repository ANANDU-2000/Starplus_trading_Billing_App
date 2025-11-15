/*
Purpose: Product model for inventory management
Author: AI Assistant
Date: 2024
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class Product
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(50)]
        public string Sku { get; set; } = string.Empty;
        [Required]
        [MaxLength(200)]
        public string NameEn { get; set; } = string.Empty;
        [MaxLength(200)]
        public string? NameAr { get; set; }
        [Required]
        [MaxLength(20)]
        public string UnitType { get; set; } = "PIECE"; // Now a string field for flexibility (CRTN, KG, PIECE, etc.)
        public decimal ConversionToBase { get; set; }
        public decimal CostPrice { get; set; }
        public decimal SellPrice { get; set; }
        public decimal StockQty { get; set; }
        public int ReorderLevel { get; set; }
        public DateTime? ExpiryDate { get; set; } // Track product expiry date
        public string? DescriptionEn { get; set; }
        public string? DescriptionAr { get; set; }
        public byte[] RowVersion { get; set; } = Array.Empty<byte>();
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}

