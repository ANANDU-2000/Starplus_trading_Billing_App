/*
Purpose: Generate unique receipt numbers in format REC-YYYY-NNNN
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;

namespace FrozenApi.Services
{
    public interface IReceiptNumberService
    {
        Task<string> GenerateNextReceiptNumberAsync();
    }

    public class ReceiptNumberService : IReceiptNumberService
    {
        private readonly AppDbContext _context;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);

        public ReceiptNumberService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GenerateNextReceiptNumberAsync()
        {
            await _semaphore.WaitAsync();
            try
            {
                var year = DateTime.UtcNow.Year;
                var prefix = $"REC-{year}-";
                var lastReceipt = await _context.PaymentReceipts
                    .Where(r => r.ReceiptNumber.StartsWith(prefix))
                    .OrderByDescending(r => r.ReceiptNumber)
                    .Select(r => r.ReceiptNumber)
                    .FirstOrDefaultAsync();

                int nextSeq = 1;
                if (!string.IsNullOrEmpty(lastReceipt) && lastReceipt.Length > prefix.Length)
                {
                    var seqPart = lastReceipt.Substring(prefix.Length);
                    if (int.TryParse(seqPart, out int lastNum))
                        nextSeq = lastNum + 1;
                }
                return $"{prefix}{nextSeq:D4}";
            }
            finally
            {
                _semaphore.Release();
            }
        }
    }
}
