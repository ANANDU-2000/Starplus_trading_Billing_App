/*
Purpose: Payment receipt record for generated payment receipts
*/
using System.ComponentModel.DataAnnotations;

namespace FrozenApi.Models
{
    public class PaymentReceipt
    {
        public int Id { get; set; }
        [Required]
        [MaxLength(30)]
        public string ReceiptNumber { get; set; } = string.Empty;
        public DateTime GeneratedAt { get; set; }
        public int GeneratedByUserId { get; set; }
        [MaxLength(500)]
        public string? PdfStoragePath { get; set; }

        public virtual User GeneratedByUser { get; set; } = null!;
        public virtual ICollection<PaymentReceiptPayment> PaymentLinks { get; set; } = new List<PaymentReceiptPayment>();
    }

    public class PaymentReceiptPayment
    {
        public int Id { get; set; }
        public int PaymentReceiptId { get; set; }
        public int PaymentId { get; set; }

        public virtual PaymentReceipt PaymentReceipt { get; set; } = null!;
        public virtual Payment Payment { get; set; } = null!;
    }
}
