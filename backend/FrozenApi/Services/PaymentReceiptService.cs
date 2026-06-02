/*
Purpose: Generate and store payment receipts
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace FrozenApi.Services
{
    public interface IPaymentReceiptService
    {
        Task<PaymentReceiptDto> GenerateReceiptAsync(int userId, int[] paymentIds);
        Task<PaymentReceiptDto?> GetReceiptByIdAsync(int receiptId);
        Task<byte[]?> GetReceiptPdfAsync(int receiptId);
        Task<List<PaymentReceiptDto>> GetReceiptsByCustomerAsync(int customerId);
    }

    public class PaymentReceiptService : IPaymentReceiptService
    {
        private readonly AppDbContext _context;
        private readonly IReceiptNumberService _receiptNumberService;

        public PaymentReceiptService(AppDbContext context, IReceiptNumberService receiptNumberService)
        {
            _context = context;
            _receiptNumberService = receiptNumberService;
            QuestPDF.Settings.License = LicenseType.Community;
            QuestPDF.Settings.CheckIfAllTextGlyphsAreAvailable = false;
        }

        public async Task<PaymentReceiptDto> GenerateReceiptAsync(int userId, int[] paymentIds)
        {
            if (paymentIds == null || paymentIds.Length == 0)
                throw new InvalidOperationException("At least one payment is required.");

            var distinctIds = paymentIds.Distinct().ToArray();
            if (distinctIds.Length != paymentIds.Length)
                throw new InvalidOperationException("Duplicate payment IDs are not allowed.");

            var payments = await _context.Payments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Where(p => paymentIds.Contains(p.Id))
                .OrderBy(p => p.PaymentDate)
                .ToListAsync();

            if (payments.Count != paymentIds.Length)
                throw new InvalidOperationException("One or more payment IDs were not found.");

            var now = DateTime.UtcNow.Date;
            var futurePayments = payments.Where(p => p.PaymentDate.Date > now).ToList();
            if (futurePayments.Any())
                throw new InvalidOperationException("Cannot generate receipt: one or more payments have a future payment date. Please correct the payment dates first.");

            var totalAmount = payments.Sum(p => p.Amount);
            if (totalAmount <= 0)
                throw new InvalidOperationException("Cannot generate receipt: total payment amount must be greater than zero.");

            var customerId = payments.First().CustomerId;
            if (!customerId.HasValue || payments.Any(p => p.CustomerId != customerId))
                throw new InvalidOperationException("All payments must belong to the same customer.");

            foreach (var p in payments)
            {
                if (p.SaleId.HasValue && (p.Sale == null || p.Sale.IsDeleted))
                    throw new InvalidOperationException("Cannot generate receipt: a payment references an invoice that is missing or deleted. Please correct the payment or invoice and try again.");
            }

            // Only reuse an existing receipt when it contains exactly the same set of payments (no more, no less).
            var existingLink = await _context.PaymentReceiptPayments
                .Where(pr => paymentIds.Contains(pr.PaymentId))
                .Select(pr => new { pr.PaymentReceiptId, pr.PaymentId })
                .ToListAsync();
            if (existingLink.Count == paymentIds.Length)
            {
                var receiptIds = existingLink.Select(x => x.PaymentReceiptId).Distinct().ToList();
                if (receiptIds.Count == 1)
                {
                    var receiptId = receiptIds[0];
                    var paymentIdsInReceipt = await _context.PaymentReceiptPayments
                        .Where(pr => pr.PaymentReceiptId == receiptId)
                        .Select(pr => pr.PaymentId)
                        .ToListAsync();
                    var requestedSet = paymentIds.OrderBy(x => x).ToList();
                    var existingSet = paymentIdsInReceipt.OrderBy(x => x).ToList();
                    if (requestedSet.SequenceEqual(existingSet))
                    {
                        var existing = await BuildReceiptDtoAsync(receiptId, true);
                        if (existing != null) return existing;
                    }
                }
            }

            var userExists = await _context.Users.AnyAsync(u => u.Id == userId);
            if (!userExists)
                throw new InvalidOperationException("User not found. Cannot generate receipt.");

            var receiptNumber = await _receiptNumberService.GenerateNextReceiptNumberAsync();

            var receipt = new PaymentReceipt
            {
                ReceiptNumber = receiptNumber,
                GeneratedAt = DateTime.UtcNow,
                GeneratedByUserId = userId
            };
            _context.PaymentReceipts.Add(receipt);

            await using (var transaction = await _context.Database.BeginTransactionAsync())
            {
                try
                {
                    await _context.SaveChangesAsync();
                    foreach (var p in payments)
                    {
                        _context.PaymentReceiptPayments.Add(new PaymentReceiptPayment
                        {
                            PaymentReceiptId = receipt.Id,
                            PaymentId = p.Id
                        });
                    }
                    await _context.SaveChangesAsync();
                    await transaction.CommitAsync();
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            }

            var built = await BuildReceiptDtoAsync(receipt.Id, false);
            if (built == null)
                throw new InvalidOperationException("Receipt was saved but could not be loaded for response.");
            return built;
        }

        public async Task<PaymentReceiptDto?> GetReceiptByIdAsync(int receiptId)
        {
            return await BuildReceiptDtoAsync(receiptId, true);
        }

        private async Task<PaymentReceiptDto?> BuildReceiptDtoAsync(int receiptId, bool isReprint)
        {
            var receipt = await _context.PaymentReceipts
                .Include(r => r.PaymentLinks)
                .ThenInclude(l => l.Payment)
                .ThenInclude(p => p!.Sale)
                .Include(r => r.PaymentLinks)
                .ThenInclude(l => l.Payment)
                .ThenInclude(p => p!.Customer)
                .FirstOrDefaultAsync(r => r.Id == receiptId);
            if (receipt == null) return null;

            var payments = receipt.PaymentLinks.Select(l => l.Payment).Where(p => p != null).Cast<Payment>().ToList();
            if (payments.Count == 0)
                return null;
            var totalAmount = payments.Sum(p => p.Amount);
            var customerId = payments.First().CustomerId;
            var customer = payments.First().Customer;

            var settings = await _context.Settings.ToDictionaryAsync(s => s.Key, s => s.Value);
            var companyNameEn = settings.GetValueOrDefault("COMPANY_NAME_EN") ?? "STARPLUS FOODSTUFF TRADING";
            var companyNameAr = settings.GetValueOrDefault("COMPANY_NAME_AR") ?? "";
            var companyAddress = settings.GetValueOrDefault("COMPANY_ADDRESS") ?? "";
            var companyTrn = settings.GetValueOrDefault("COMPANY_TRN") ?? "";
            var companyPhone = settings.GetValueOrDefault("COMPANY_PHONE") ?? "";

            // Previous/remaining balance: Sales - CLEARED payments - Sales returns (matches BalanceService and CustomerService)
            decimal previousBalance = 0;
            decimal currentTotalOutstanding = 0;
            var currentOutstandingAsOfDate = DateTime.UtcNow.Date;
            int pendingBillsCount = 0;
            if (customerId.HasValue)
            {
                var asOfDate = payments.Min(p => p.PaymentDate);
                var salesBefore = await _context.Sales
                    .Where(s => s.CustomerId == customerId && !s.IsDeleted && s.InvoiceDate <= asOfDate)
                    .SumAsync(s => s.GrandTotal);
                var paymentsBefore = await _context.Payments
                    .Where(p => p.CustomerId == customerId && p.PaymentDate < asOfDate && p.Status == PaymentStatus.CLEARED)
                    .SumAsync(p => p.Amount);
                var returnsBefore = await _context.SaleReturns
                    .Where(sr => sr.CustomerId == customerId && sr.ReturnDate < asOfDate)
                    .SumAsync(sr => sr.GrandTotal);
                previousBalance = salesBefore - paymentsBefore - returnsBefore;

                // Current total outstanding (as of today): same formula as BalanceService / CustomerService
                var totalSales = await _context.Sales
                    .Where(s => s.CustomerId == customerId && !s.IsDeleted)
                    .SumAsync(s => (decimal?)s.GrandTotal) ?? 0m;
                var totalPayments = await _context.Payments
                    .Where(p => p.CustomerId == customerId && p.Status == PaymentStatus.CLEARED)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                var totalReturns = await _context.SaleReturns
                    .Where(sr => sr.CustomerId == customerId)
                    .SumAsync(sr => (decimal?)sr.GrandTotal) ?? 0m;
                currentTotalOutstanding = totalSales - totalPayments - totalReturns;

                // Pending bills: count of invoices that still have outstanding balance
                var customerSales = await _context.Sales
                    .Where(s => s.CustomerId == customerId && !s.IsDeleted)
                    .Select(s => new { s.Id, s.GrandTotal })
                    .ToListAsync();
                var paidBySale = await _context.Payments
                    .Where(p => p.CustomerId == customerId && p.SaleId != null && p.Status == PaymentStatus.CLEARED)
                    .GroupBy(p => p.SaleId!.Value)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(p => p.Amount) })
                    .ToDictionaryAsync(x => x.SaleId, x => x.Total);
                var returnsBySale = await _context.SaleReturns
                    .Where(sr => sr.CustomerId == customerId)
                    .GroupBy(sr => sr.SaleId)
                    .Select(g => new { SaleId = g.Key, Total = g.Sum(sr => sr.GrandTotal) })
                    .ToDictionaryAsync(x => x.SaleId, x => x.Total);
                foreach (var s in customerSales)
                {
                    var paid = paidBySale.GetValueOrDefault(s.Id, 0m);
                    var returns = returnsBySale.GetValueOrDefault(s.Id, 0m);
                    var outstanding = s.GrandTotal - paid - returns;
                    if (outstanding > 0.001m) pendingBillsCount++;
                }
            }
            var remainingBalance = previousBalance - totalAmount;

            var invoiceLines = new List<PaymentReceiptInvoiceLineDto>();
            foreach (var p in payments)
            {
                if (p.Sale != null)
                    invoiceLines.Add(new PaymentReceiptInvoiceLineDto
                    {
                        InvoiceNo = p.Sale.InvoiceNo,
                        InvoiceDate = p.Sale.InvoiceDate,
                        InvoiceTotal = p.Sale.GrandTotal,
                        AmountApplied = p.Amount
                    });
                else
                    invoiceLines.Add(new PaymentReceiptInvoiceLineDto
                    {
                        InvoiceNo = "Advance / On Account",
                        InvoiceDate = p.PaymentDate,
                        InvoiceTotal = 0,
                        AmountApplied = p.Amount
                    });
            }

            return new PaymentReceiptDto
            {
                Id = receipt.Id,
                ReceiptNumber = receipt.ReceiptNumber,
                GeneratedAt = receipt.GeneratedAt,
                TotalAmount = totalAmount,
                AmountInWords = AmountToWords(totalAmount),
                CustomerName = customer?.Name ?? "N/A",
                CustomerTrn = customer?.Trn,
                CustomerAddress = customer?.Address,
                CompanyNameEn = companyNameEn,
                CompanyNameAr = companyNameAr,
                CompanyAddress = companyAddress,
                CompanyTrn = companyTrn,
                CompanyPhone = companyPhone,
                Payments = payments.Select(p => new PaymentReceiptPaymentLineDto
                {
                    PaymentId = p.Id,
                    Amount = p.Amount,
                    Method = p.Mode.ToString(),
                    Reference = p.Reference,
                    PaymentDate = p.PaymentDate
                }).ToList(),
                Invoices = invoiceLines,
                PreviousBalance = previousBalance,
                AmountPaid = totalAmount,
                RemainingBalance = remainingBalance,
                CurrentTotalOutstanding = currentTotalOutstanding,
                CurrentOutstandingAsOfDate = currentOutstandingAsOfDate,
                PendingBillsCount = pendingBillsCount,
                IsReprint = isReprint
            };
        }

        private static string AmountToWords(decimal amount)
        {
            var whole = (int)Math.Floor(amount);
            var frac = (int)Math.Round((amount - whole) * 100);
            if (whole == 0 && frac == 0) return "Zero Dirhams Only";
            var s = NumberToWords(whole) + " Dirhams";
            if (frac > 0) s += " and " + NumberToWords(frac) + " Fils";
            return s + " Only";
        }

        private static string NumberToWords(int n)
        {
            if (n == 0) return "Zero";
            var units = new[] { "", "One", "Two", "Three", "Four", "Five", "Six", "Seven", "Eight", "Nine" };
            var teens = new[] { "Ten", "Eleven", "Twelve", "Thirteen", "Fourteen", "Fifteen", "Sixteen", "Seventeen", "Eighteen", "Nineteen" };
            var tens = new[] { "", "", "Twenty", "Thirty", "Forty", "Fifty", "Sixty", "Seventy", "Eighty", "Ninety" };
            if (n < 10) return units[n];
            if (n < 20) return teens[n - 10];
            if (n < 100) return tens[n / 10] + (n % 10 > 0 ? " " + units[n % 10] : "");
            if (n < 1000) return units[n / 100] + " Hundred" + (n % 100 > 0 ? " " + NumberToWords(n % 100) : "");
            if (n < 1000000) return NumberToWords(n / 1000) + " Thousand" + (n % 1000 > 0 ? " " + NumberToWords(n % 1000) : "");
            return NumberToWords(n / 1000000) + " Million" + (n % 1000000 > 0 ? " " + NumberToWords(n % 1000000) : "");
        }

        public async Task<byte[]?> GetReceiptPdfAsync(int receiptId)
        {
            var dto = await GetReceiptByIdAsync(receiptId);
            if (dto == null) return null;
            return await GenerateReceiptPdfAsync(dto);
        }

        private Task<byte[]> GenerateReceiptPdfAsync(PaymentReceiptDto dto)
        {
            var invoiceRows = dto.Invoices
                .Where(inv => inv.InvoiceNo != "Advance / On Account")
                .OrderBy(inv => inv.InvoiceDate)
                .ThenBy(inv => inv.InvoiceNo)
                .ToList();
            var advanceRows = dto.Invoices
                .Where(inv => inv.InvoiceNo == "Advance / On Account")
                .OrderBy(inv => inv.InvoiceDate)
                .ToList();
            var sortedInvoices = invoiceRows.Concat(advanceRows).ToList();
            if (sortedInvoices.Count == 0)
            {
                sortedInvoices.Add(new PaymentReceiptInvoiceLineDto
                {
                    InvoiceNo = "Payment on Account",
                    InvoiceDate = dto.GeneratedAt,
                    InvoiceTotal = 0,
                    AmountApplied = dto.TotalAmount
                });
            }

            var paymentMethods = string.Join(", ", dto.Payments.Select(p => p.Method).Distinct());
            var paymentDates = string.Join(", ", dto.Payments.Select(p => p.PaymentDate.ToString("dd-MM-yyyy")).Distinct());

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(36);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    page.Header().Column(col =>
                    {
                        col.Item().AlignCenter().Text("PAYMENT RECEIPT").Bold().FontSize(16);
                        col.Item().AlignCenter().Text("Receipt / Payment Acknowledgement").FontSize(9);
                        col.Item().PaddingTop(8).Row(row =>
                        {
                            row.RelativeItem().Text($"Receipt No: {dto.ReceiptNumber}");
                            row.RelativeItem().AlignRight().Text($"Date: {dto.GeneratedAt:dd-MM-yyyy}");
                        });
                        col.Item().PaddingTop(6).Text(dto.CompanyNameEn).Bold();
                        if (!string.IsNullOrWhiteSpace(dto.CompanyAddress))
                            col.Item().Text(dto.CompanyAddress);
                        col.Item().Text($"TRN: {dto.CompanyTrn}  |  Phone: {dto.CompanyPhone}");
                    });

                    page.Content().PaddingVertical(12).Column(col =>
                    {
                        col.Item().Text("Received From").Bold();
                        col.Item().Text(dto.CustomerName);
                        col.Item().Text($"TRN: {dto.CustomerTrn ?? "-"}");
                        if (!string.IsNullOrWhiteSpace(dto.CustomerAddress))
                            col.Item().Text(dto.CustomerAddress);

                        col.Item().PaddingTop(8).Text($"Payment Method: {paymentMethods}");
                        col.Item().Text($"Payment Date(s): {paymentDates}");

                        col.Item().PaddingTop(12).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(2);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                                columns.RelativeColumn(1.5f);
                            });

                            table.Header(header =>
                            {
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Invoice No").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).Text("Invoice Date").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Invoice Total").Bold();
                                header.Cell().Border(1).Background(Colors.Grey.Lighten3).Padding(4).AlignRight().Text("Paid Amount").Bold();
                            });

                            foreach (var inv in sortedInvoices)
                            {
                                table.Cell().Border(1).Padding(4).Text(inv.InvoiceNo);
                                table.Cell().Border(1).Padding(4).Text(inv.InvoiceDate.ToString("dd-MM-yyyy"));
                                table.Cell().Border(1).Padding(4).AlignRight().Text(inv.InvoiceTotal.ToString("N2"));
                                table.Cell().Border(1).Padding(4).AlignRight().Text(inv.AmountApplied.ToString("N2"));
                            }
                        });

                        col.Item().PaddingTop(10).Text($"Total Paid: {dto.TotalAmount:N2} AED").Bold().FontSize(12);
                        col.Item().PaddingTop(4).Text($"Amount in words: {dto.AmountInWords}");
                        col.Item().PaddingTop(8).Text($"Previous Balance: {dto.PreviousBalance:N2} AED");
                        col.Item().Text($"Remaining Balance: {dto.RemainingBalance:N2} AED");
                    });

                    page.Footer().Column(col =>
                    {
                        col.Item().PaddingTop(24).Text("Received by: _______________________");
                        col.Item().PaddingTop(4).Text($"For {dto.CompanyNameEn}");
                        col.Item().PaddingTop(8).Text("This is a computer generated receipt.").FontSize(8).FontColor(Colors.Grey.Medium);
                        if (dto.IsReprint)
                            col.Item().Text("REPRINT").Bold().FontColor(Colors.Red.Medium);
                    });
                });
            });

            return Task.FromResult(document.GeneratePdf());
        }

        public async Task<List<PaymentReceiptDto>> GetReceiptsByCustomerAsync(int customerId)
        {
            var receiptIds = await _context.PaymentReceiptPayments
                .Include(pr => pr.Payment)
                .Where(pr => pr.Payment != null && pr.Payment.CustomerId == customerId)
                .Select(pr => pr.PaymentReceiptId)
                .Distinct()
                .ToListAsync();
            var list = new List<PaymentReceiptDto>();
            foreach (var id in receiptIds)
            {
                var dto = await BuildReceiptDtoAsync(id, true);
                if (dto != null) list.Add(dto);
            }
            return list.OrderByDescending(r => r.GeneratedAt).ToList();
        }
    }
}
