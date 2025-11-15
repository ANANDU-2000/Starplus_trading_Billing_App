/*
Purpose: Invoice number management service with auto-generation and manual edit support
Author: AI Assistant
Date: 2025
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;
using System.Text.RegularExpressions;
using Npgsql;

namespace FrozenApi.Services
{
    public interface IInvoiceNumberService
    {
        Task<string> GenerateNextInvoiceNumberAsync();
        Task<bool> ValidateInvoiceNumberAsync(string invoiceNumber, int? excludeSaleId = null);
        Task<string> FormatInvoiceNumberAsync(int number);
        Task<bool> IsInvoiceNumberDuplicateAsync(string invoiceNumber, int? excludeSaleId = null);
    }

    public class InvoiceNumberService : IInvoiceNumberService
    {
        private readonly AppDbContext _context;
        private readonly IConfiguration _configuration;
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(1, 1);
        private static readonly Random _random = new Random();

        public InvoiceNumberService(AppDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<string> GenerateNextInvoiceNumberAsync()
        {
            // CRITICAL FIX: Use direct database connection to avoid transaction conflicts
            await _semaphore.WaitAsync();
            try
            {
                var connectionString = _configuration.GetConnectionString("DefaultConnection");
                
                // Use separate connection to avoid transaction conflicts
                using (var connection = new NpgsqlConnection(connectionString))
                {
                    await connection.OpenAsync();
                    
                    using (var command = new NpgsqlCommand("SELECT nextval('invoice_number_seq')", connection))
                    {
                        var result = await command.ExecuteScalarAsync();
                        if (result != null && long.TryParse(result.ToString(), out long nextNumber))
                        {
                            Console.WriteLine($"🔢 PostgreSQL sequence generated: {nextNumber}");
                            return await FormatInvoiceNumberAsync((int)nextNumber);
                        }
                    }
                }
                
                // If sequence query fails, use fallback
                Console.WriteLine("⚠️ Sequence query returned null, using fallback");
                return await GenerateNextInvoiceNumberFallbackAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Error generating invoice number from sequence: {ex.Message}");
                
                // Fallback to old method if sequence fails
                return await GenerateNextInvoiceNumberFallbackAsync();
            }
            finally
            {
                _semaphore.Release();
            }
        }
        
        private async Task<string> GenerateNextInvoiceNumberFallbackAsync()
        {
            // CLIENT REQUIREMENT: Invoice numbers start from 2000
            const int STARTING_INVOICE_NUMBER = 2000;

            // Add small random delay to reduce collision probability (0-20ms)
            await Task.Delay(_random.Next(0, 20));

            // Get the highest invoice number (by ID to ensure we get the latest)
            var lastSale = await _context.Sales
                .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.InvoiceNo))
                .OrderByDescending(s => s.Id)
                .FirstOrDefaultAsync();

            int nextNumber = STARTING_INVOICE_NUMBER; // Start from 2000

            if (lastSale != null && !string.IsNullOrEmpty(lastSale.InvoiceNo))
            {
                // Extract number from invoice (handles formats like INV-0001, 0001, etc.)
                var invoiceNo = lastSale.InvoiceNo.Trim();
                
                // Remove common prefixes
                var cleanNumber = invoiceNo;
                if (invoiceNo.Contains("-"))
                {
                    var parts = invoiceNo.Split('-');
                    cleanNumber = parts.LastOrDefault() ?? invoiceNo;
                }

                // Extract numeric part
                var numericPart = new string(cleanNumber.Where(char.IsDigit).ToArray());
                if (!string.IsNullOrEmpty(numericPart) && int.TryParse(numericPart, out int extractedNum))
                {
                    // Ensure we don't go below 2000
                    nextNumber = Math.Max(extractedNum + 1, STARTING_INVOICE_NUMBER);
                }
                else
                {
                    // OPTIMIZED: Instead of loading all invoices, use a more efficient approach
                    // Get the highest ID first (assuming newer invoices have higher IDs)
                    var highestIdSale = await _context.Sales
                        .Where(s => !s.IsDeleted && !string.IsNullOrEmpty(s.InvoiceNo))
                        .OrderByDescending(s => s.Id)
                        .Select(s => s.InvoiceNo)
                        .Take(100) // Only check last 100 invoices for performance
                        .ToListAsync();

                    int maxNumber = STARTING_INVOICE_NUMBER - 1;
                    foreach (var inv in highestIdSale)
                    {
                        var numPart = new string(inv.Where(char.IsDigit).ToArray());
                        if (int.TryParse(numPart, out int num))
                        {
                            maxNumber = Math.Max(maxNumber, num);
                        }
                    }
                    // Ensure next number is at least 2000
                    nextNumber = Math.Max(maxNumber + 1, STARTING_INVOICE_NUMBER);
                }
            }

            return await FormatInvoiceNumberAsync(nextNumber);
        }

        public Task<string> FormatInvoiceNumberAsync(int number)
        {
            // Format as 0001, 0002, etc. (no INV prefix)
            return Task.FromResult($"{number:D4}");
        }

        public async Task<bool> ValidateInvoiceNumberAsync(string invoiceNumber, int? excludeSaleId = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return false;

            // Check format: #### (4 or more digits, no prefix required)
            var isValidFormat = Regex.IsMatch(invoiceNumber.Trim(), @"^\d{4,}$", RegexOptions.IgnoreCase);
            if (!isValidFormat)
                return false;

            // Extract numeric value to check minimum
            var numericPart = new string(invoiceNumber.Trim().Where(char.IsDigit).ToArray());
            if (int.TryParse(numericPart, out int invoiceNum))
            {
                // CLIENT REQUIREMENT: Invoice numbers must be >= 2000
                if (invoiceNum < 2000)
                    return false;
            }

            // Check for duplicates
            var isDuplicate = await IsInvoiceNumberDuplicateAsync(invoiceNumber, excludeSaleId);
            return !isDuplicate;
        }

        public async Task<bool> IsInvoiceNumberDuplicateAsync(string invoiceNumber, int? excludeSaleId = null)
        {
            if (string.IsNullOrWhiteSpace(invoiceNumber))
                return false;

            var normalizedInvoice = invoiceNumber.Trim();

            var query = _context.Sales
                .Where(s => !s.IsDeleted && s.InvoiceNo == normalizedInvoice);

            if (excludeSaleId.HasValue)
            {
                query = query.Where(s => s.Id != excludeSaleId.Value);
            }

            return await query.AnyAsync();
        }
    }
}

