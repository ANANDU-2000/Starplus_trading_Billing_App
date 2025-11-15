/*
Purpose: Sale service for POS billing and invoice management
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FrozenApi.Data;
using FrozenApi.Models;
using FrozenApi.Helpers;
using System.Text.Json;

namespace FrozenApi.Services
{
    public interface ISaleService
    {
        Task<PagedResponse<SaleDto>> GetSalesAsync(int page = 1, int pageSize = 10, string? search = null);
        Task<SaleDto?> GetSaleByIdAsync(int id);
        Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, int userId);
        Task<SaleDto> CreateSaleWithOverrideAsync(CreateSaleRequest request, string reason, int userId);
        Task<SaleDto> UpdateSaleAsync(int saleId, CreateSaleRequest request, int userId, string? editReason = null, byte[]? expectedRowVersion = null);
        Task<bool> DeleteSaleAsync(int saleId, int userId);
        Task<string> GenerateInvoiceNumberAsync();
        Task<byte[]> GenerateInvoicePdfAsync(int saleId);
        Task<bool> CanEditInvoiceAsync(int saleId, int userId, string userRole);
        Task<bool> UnlockInvoiceAsync(int saleId, int userId, string unlockReason);
        Task<List<InvoiceVersion>> GetInvoiceVersionsAsync(int saleId);
        Task<SaleDto?> RestoreInvoiceVersionAsync(int saleId, int versionNumber, int userId);
        Task<bool> LockOldInvoicesAsync(); // Background job to lock invoices after 48 hours
        Task<List<SaleDto>> GetDeletedSalesAsync(); // Get all deleted sales for audit trail
    }

    public class SaleService : ISaleService
    {
        private readonly AppDbContext _context;
        private readonly IPdfService _pdfService;
        private readonly IComprehensiveBackupService _backupService;
        private readonly IInvoiceNumberService _invoiceNumberService;
        private readonly IValidationService _validationService;
        private readonly IAlertService _alertService;
        private readonly IBalanceService _balanceService;
        private readonly ITimeZoneService _timeZoneService;

        public SaleService(
            AppDbContext context, 
            IPdfService pdfService, 
            IComprehensiveBackupService backupService,
            IInvoiceNumberService invoiceNumberService,
            IValidationService validationService,
            IAlertService alertService,
            IBalanceService balanceService,
            ITimeZoneService timeZoneService)
        {
            _context = context;
            _pdfService = pdfService;
            _backupService = backupService;
            _invoiceNumberService = invoiceNumberService;
            _validationService = validationService;
            _alertService = alertService;
            _balanceService = balanceService;
            _timeZoneService = timeZoneService;
        }

        public async Task<PagedResponse<SaleDto>> GetSalesAsync(int page = 1, int pageSize = 10, string? search = null)
        {
            try
            {
                // OPTIMIZATION: Use AsNoTracking for read-only queries and limit page size
                pageSize = Math.Min(pageSize, 100); // Max 100 items per page for performance
                
                var query = _context.Sales
                    .AsNoTracking() // Performance: No change tracking needed for read-only
                    .Include(s => s.Customer)
                    .Include(s => s.Items)
                        .ThenInclude(i => i.Product)
                    .Include(s => s.CreatedByUser)
                    .Include(s => s.LastModifiedByUser)
                    .AsQueryable();

                // Filter deleted sales (after migration)
                query = query.Where(s => !s.IsDeleted);

                if (!string.IsNullOrEmpty(search))
                {
                    query = query.Where(s => s.InvoiceNo.Contains(search) || 
                                           (s.Customer != null && s.Customer.Name.Contains(search)));
                }

                var totalCount = await query.CountAsync();
                
                // Load into memory first to avoid database column issues
                var salesList = await query
                    .OrderByDescending(s => s.InvoiceDate)
                    .Skip((page - 1) * pageSize)
                    .Take(pageSize)
                    .ToListAsync();

                var sales = salesList.Select(s => new SaleDto
                {
                    Id = s.Id,
                    InvoiceNo = s.InvoiceNo,
                    InvoiceDate = s.InvoiceDate,
                    CustomerId = s.CustomerId,
                    CustomerName = s.Customer != null ? s.Customer.Name : null,
                    Subtotal = s.Subtotal,
                    VatTotal = s.VatTotal,
                    Discount = s.Discount,
                    GrandTotal = s.GrandTotal,
                    PaymentStatus = s.PaymentStatus.ToString(),
                    Notes = s.Notes,
                    Items = s.Items.Select(i => new SaleItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product?.NameEn ?? "Unknown",
                        UnitType = i.UnitType,
                        Qty = i.Qty,
                        UnitPrice = i.UnitPrice,
                        Discount = i.Discount,
                        VatAmount = i.VatAmount,
                        LineTotal = i.LineTotal
                    }).ToList(),
                    CreatedAt = s.CreatedAt,
                    CreatedBy = s.CreatedByUser != null ? s.CreatedByUser.Name : "Unknown",
                    Version = s.Version,
                    IsLocked = s.IsLocked,
                    LastModifiedAt = s.LastModifiedAt,
                    LastModifiedBy = s.LastModifiedByUser != null ? s.LastModifiedByUser.Name : null,
                    RowVersion = s.RowVersion != null && s.RowVersion.Length > 0 
                        ? Convert.ToBase64String(s.RowVersion) 
                        : null
                }).ToList();

                return new PagedResponse<SaleDto>
                {
                    Items = sales,
                    TotalCount = totalCount,
                    Page = page,
                    PageSize = pageSize,
                    TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
                };
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ GetSalesAsync Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        public async Task<SaleDto?> GetSaleByIdAsync(int id)
        {
            // CRITICAL: Use AsNoTracking for read-only queries (performance optimization)
            var sale = await _context.Sales
                .AsNoTracking()
                .Include(s => s.Customer)
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(s => s.Id == id && !s.IsDeleted); // Exclude deleted sales for normal operations

            if (sale == null) return null;

            return new SaleDto
            {
                Id = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                InvoiceDate = sale.InvoiceDate,
                CustomerId = sale.CustomerId,
                CustomerName = sale.Customer?.Name,
                Subtotal = sale.Subtotal,
                VatTotal = sale.VatTotal,
                Discount = sale.Discount,
                GrandTotal = sale.GrandTotal,
                PaymentStatus = sale.PaymentStatus.ToString(),
                Notes = sale.Notes,
                Items = sale.Items.Select(i => new SaleItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.NameEn,
                    UnitType = string.IsNullOrWhiteSpace(i.UnitType) ? "CRTN" : i.UnitType.ToUpper(), // Ensure UnitType is never null
                    Qty = i.Qty,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    VatAmount = i.VatAmount,
                    LineTotal = i.LineTotal
                }).ToList(),
                CreatedAt = sale.CreatedAt,
                CreatedBy = sale.CreatedByUser?.Name ?? "Unknown",
                Version = sale.Version,
                IsLocked = sale.IsLocked,
                LastModifiedAt = sale.LastModifiedAt,
                LastModifiedBy = sale.LastModifiedByUser?.Name,
                RowVersion = sale.RowVersion != null && sale.RowVersion.Length > 0 
                    ? Convert.ToBase64String(sale.RowVersion) 
                    : null
            };
        }

        public async Task<SaleDto> CreateSaleAsync(CreateSaleRequest request, int userId)
        {
            // Retry logic for invoice number conflicts (race condition handling)
            const int maxRetries = 5;
            Exception? lastException = null;
            
            for (int retryCount = 0; retryCount < maxRetries; retryCount++)
            {
                try
                {
                    return await CreateSaleInternalAsync(request, userId);
                }
                catch (DbUpdateException ex) when (IsInvoiceNumberConflict(ex))
                {
                    lastException = ex;
                    
                    if (retryCount < maxRetries - 1)
                    {
                        // Race condition: Another transaction saved the same invoice number
                        Console.WriteLine($"⚠️ Invoice number conflict detected (attempt {retryCount + 1}/{maxRetries}). Retrying with new number...");
                        
                        // Clear the invoice number to force regeneration
                        request.InvoiceNo = null;
                        
                        // Exponential backoff: 50ms, 100ms, 200ms, 400ms
                        await Task.Delay(50 * (int)Math.Pow(2, retryCount));
                    }
                }
                catch (Exception ex)
                {
                    // Log non-duplicate errors
                    Console.WriteLine($"❌ CreateSaleAsync Error: {ex.GetType().Name}");
                    Console.WriteLine($"❌ Message: {ex.Message}");
                    Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                    if (ex.InnerException != null)
                    {
                        Console.WriteLine($"❌ Inner Exception: {ex.InnerException.GetType().Name}");
                        Console.WriteLine($"❌ Inner Message: {ex.InnerException.Message}");
                        Console.WriteLine($"❌ Inner Stack Trace: {ex.InnerException.StackTrace}");
                    }
                    throw; // Re-throw non-duplicate errors immediately
                }
            }
            
            // All retries exhausted
            Console.WriteLine($"❌ Failed to create sale after {maxRetries} attempts due to invoice number conflicts.");
            throw new InvalidOperationException(
                "Unable to generate unique invoice number after multiple attempts. This may be due to high concurrent activity. Please try again.",
                lastException
            );
        }
        
        // Helper method to detect invoice number conflict errors
        private bool IsInvoiceNumberConflict(DbUpdateException ex)
        {
            if (ex.InnerException == null) return false;
            
            var innerMessage = ex.InnerException.Message ?? string.Empty;
            return innerMessage.Contains("IX_Sales_InvoiceNo", StringComparison.OrdinalIgnoreCase) ||
                   innerMessage.Contains("duplicate key", StringComparison.OrdinalIgnoreCase);
        }
        
        private async Task<SaleDto> CreateSaleInternalAsync(CreateSaleRequest request, int userId)
        {
            // Log incoming request for debugging
            Console.WriteLine($"📝 CreateSaleInternalAsync called with InvoiceNo: '{request.InvoiceNo ?? "NULL"}'" );
            
            // CRITICAL FIX: Generate invoice number BEFORE starting transaction
            // to avoid "transaction aborted" errors
            string invoiceNo;
            if (!string.IsNullOrWhiteSpace(request.InvoiceNo))
            {
                Console.WriteLine($"🔢 Frontend provided invoice number: {request.InvoiceNo}");
                invoiceNo = request.InvoiceNo.Trim();
            }
            else
            {
                // Auto-generate invoice number OUTSIDE transaction
                invoiceNo = await _invoiceNumberService.GenerateNextInvoiceNumberAsync();
                Console.WriteLine($"🔢 Auto-generated invoice number: {invoiceNo}");
            }
            
            // Use serializable isolation to prevent concurrent invoice number conflicts
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // IDEMPOTENCY CHECK: If ExternalReference provided, check for duplicate
                if (!string.IsNullOrWhiteSpace(request.ExternalReference))
                {
                    var existingSale = await _context.Sales
                        .FirstOrDefaultAsync(s => s.ExternalReference == request.ExternalReference && !s.IsDeleted);
                    if (existingSale != null)
                    {
                        Console.WriteLine($"⚠️ Duplicate external reference detected: {request.ExternalReference}. Returning existing sale ID: {existingSale.Id}");
                        return await GetSaleByIdAsync(existingSale.Id);
                    }
                }

                // Check if invoice number already exists (moved inside transaction for safety)
                var duplicateInvoice = await _context.Sales
                    .FirstOrDefaultAsync(s => s.InvoiceNo == invoiceNo && !s.IsDeleted);
                
                if (duplicateInvoice != null)
                {
                    Console.WriteLine($"❌ Invoice {invoiceNo} already exists (ID: {duplicateInvoice.Id}). Throwing error to trigger retry.");
                    
                    // Send admin alert
                    await _alertService.CreateAlertAsync(
                        AlertType.DuplicateInvoice,
                        "Duplicate Invoice Number",
                        $"Invoice number {invoiceNo} already exists (Sale ID: {duplicateInvoice.Id})",
                        AlertSeverity.Error
                    );
                    
                    // Throw to trigger retry with new number
                    throw new DbUpdateException("Duplicate invoice number", new Exception("IX_Sales_InvoiceNo"));
                }
                
                // Validate invoice number format if manually provided
                if (!string.IsNullOrWhiteSpace(request.InvoiceNo))
                {
                    var isValid = await _invoiceNumberService.ValidateInvoiceNumberAsync(invoiceNo);
                    if (!isValid)
                    {
                        throw new InvalidOperationException($"Invoice number '{invoiceNo}' is invalid. Please use a different number.");
                    }
                }
                Console.WriteLine($"✅ Using invoice number: {invoiceNo}");

                // Calculate totals
                var vatPercent = await GetVatPercentAsync();
                decimal subtotal = 0;
                decimal vatTotal = 0;

                var saleItems = new List<SaleItem>();
                var inventoryTransactions = new List<InventoryTransaction>();

                // Use validation service for robust validation
                var validationErrors = new List<string>();
                foreach (var item in request.Items)
                {
                    // Validate quantity
                    var qtyResult = await _validationService.ValidateQuantityAsync(item.Qty);
                    if (!qtyResult.IsValid)
                    {
                        validationErrors.AddRange(qtyResult.Errors.Select(e => $"Item {item.ProductId}: {e}"));
                    }

                    // Validate price
                    var priceResult = await _validationService.ValidatePriceAsync(item.UnitPrice);
                    if (!priceResult.IsValid)
                    {
                        validationErrors.AddRange(priceResult.Errors.Select(e => $"Item {item.ProductId}: {e}"));
                    }

                    // Validate stock availability
                    var stockResult = await _validationService.ValidateStockAvailabilityAsync(item.ProductId, item.Qty);
                    if (!stockResult.IsValid)
                    {
                        validationErrors.AddRange(stockResult.Errors);
                    }
                    else if (stockResult.Warnings.Any())
                    {
                        // Log warnings but don't fail
                        foreach (var warning in stockResult.Warnings)
                        {
                            Console.WriteLine($"⚠️ Stock Warning: {warning}");
                        }
                    }
                }

                if (validationErrors.Any())
                {
                    throw new InvalidOperationException(string.Join("\n", validationErrors));
                }

                foreach (var item in request.Items)
                {
                    // Get product (Note: SQLite doesn't support FOR UPDATE lock, but transactions provide isolation)
                    var product = await _context.Products
                        .FirstOrDefaultAsync(p => p.Id == item.ProductId);

                    if (product == null)
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found");

                    // Calculate base quantity
                    var baseQty = item.Qty * product.ConversionToBase;

                    // Stock already validated above, but double-check for safety
                    if (product.StockQty < baseQty)
                    {
                        throw new InvalidOperationException($"Insufficient stock for {product.NameEn}. Available: {product.StockQty}, Required: {baseQty}");
                    }

                    // Calculate line totals: Total = qty × price, VAT = Total × 5%, Amount = Total + VAT
                    var rowTotal = item.UnitPrice * item.Qty;
                    var vatAmount = Math.Round(rowTotal * (vatPercent / 100), 2);
                    var lineAmount = rowTotal + vatAmount;

                    subtotal += rowTotal;
                    vatTotal += vatAmount;

                    // Create sale item
                    var saleItem = new SaleItem
                    {
                        ProductId = item.ProductId,
                        UnitType = string.IsNullOrWhiteSpace(item.UnitType) ? "CRTN" : item.UnitType.ToUpper(), // Ensure UnitType is never null or empty
                        Qty = item.Qty,
                        UnitPrice = item.UnitPrice,
                        Discount = 0, // No per-item discount
                        VatAmount = vatAmount,
                        LineTotal = lineAmount
                    };

                    saleItems.Add(saleItem);

                    // CRITICAL: Decrement stock only when invoice is finalized
                    // Stock is decremented atomically in this transaction
                    // If transaction fails/rolls back, stock is automatically restored
                    product.StockQty -= baseQty;
                    product.UpdatedAt = DateTime.UtcNow;

                    // Create inventory transaction
                    var inventoryTransaction = new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        ChangeQty = -baseQty,
                        TransactionType = TransactionType.Sale,
                        RefId = null, // Will be updated after sale is created
                        CreatedAt = DateTime.UtcNow
                    };

                    inventoryTransactions.Add(inventoryTransaction);
                }

                // Apply global discount (subtract from subtotal + VAT)
                var grandTotal = Math.Round((subtotal + vatTotal - request.Discount), 2);

                // Create sale
                // CRITICAL: Stock is decremented in this transaction (lines 276-290)
                // IsFinalized = true means stock has been decremented and invoice is finalized
                
                // CASH CUSTOMER LOGIC: If no customer ID (cash customer), auto-mark as paid with cash payment
                bool isCashCustomer = !request.CustomerId.HasValue;
                decimal initialPaidAmount = 0;
                SalePaymentStatus initialPaymentStatus = SalePaymentStatus.Pending;
                
                if (isCashCustomer)
                {
                    // Cash customer = instant payment, mark as paid immediately
                    initialPaidAmount = grandTotal;
                    initialPaymentStatus = SalePaymentStatus.Paid;
                }
                
                var sale = new Sale
                {
                    InvoiceNo = invoiceNo,
                    ExternalReference = request.ExternalReference, // Set external reference for idempotency
                    InvoiceDate = request.InvoiceDate ?? _timeZoneService.ConvertToUtc(_timeZoneService.GetCurrentTime()), // Use GST time, store as UTC
                    CustomerId = request.CustomerId,
                    Subtotal = subtotal,
                    VatTotal = vatTotal,
                    Discount = request.Discount,
                    GrandTotal = grandTotal,
                    TotalAmount = grandTotal, // Set TotalAmount = GrandTotal
                    PaidAmount = initialPaidAmount, // Cash customer = paid immediately
                    PaymentStatus = initialPaymentStatus, // Cash customer = Paid status
                    IsFinalized = true, // Invoice is finalized immediately - stock decremented in transaction
                    Notes = request.Notes,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                // Update sale items with sale ID
                foreach (var item in saleItems)
                {
                    item.SaleId = sale.Id;
                }

                _context.SaleItems.AddRange(saleItems);

                // Update inventory transactions with sale ID
                foreach (var invTx in inventoryTransactions)
                {
                    invTx.RefId = sale.Id;
                }

                _context.InventoryTransactions.AddRange(inventoryTransactions);

                // Process payments if provided (e.g., cash/online payment at POS)
                decimal totalPaid = 0;
                if (request.Payments != null && request.Payments.Any())
                {
                    // DUPLICATE CASH PAYMENT PREVENTION
                    var cashPayments = request.Payments.Where(p => p.Method.ToUpper() == "CASH").ToList();
                    if (cashPayments.Count > 1)
                    {
                        // Send admin alert
                        await _alertService.CreateAlertAsync(
                            AlertType.DuplicatePayment,
                            "Duplicate Cash Payment",
                            $"Multiple cash payments detected for invoice {invoiceNo}. Only first payment will be processed.",
                            AlertSeverity.Warning
                        );
                        
                        // Keep only first cash payment
                        var firstCash = cashPayments.First();
                        request.Payments = request.Payments.Where(p => p != firstCash || !cashPayments.Skip(1).Contains(p)).ToList();
                    }
                    
                    foreach (var paymentRequest in request.Payments)
                    {
                        var paymentMode = Enum.Parse<PaymentMode>(paymentRequest.Method.ToUpper());
                        var paymentStatus = paymentMode == PaymentMode.CHEQUE 
                            ? PaymentStatus.PENDING 
                            : (paymentMode == PaymentMode.CASH || paymentMode == PaymentMode.ONLINE 
                                ? PaymentStatus.CLEARED 
                                : PaymentStatus.PENDING);

                        var paymentDate = DateTime.UtcNow;
                        
                        // Create payment using EF Core (PostgreSQL compatible)
                        var payment = new Payment
                        {
                            SaleId = sale.Id,
                            CustomerId = request.CustomerId,
                            Amount = paymentRequest.Amount,
                            Mode = paymentMode,
                            Reference = paymentRequest.Ref,
                            Status = paymentStatus,
                            PaymentDate = paymentDate,
                            CreatedBy = userId,
                            CreatedAt = paymentDate,
                            UpdatedAt = paymentDate,
                            RowVersion = new byte[0]
                        };
                        
                        _context.Payments.Add(payment);
                        await _context.SaveChangesAsync(); // Save to get payment ID generated
                        
                        totalPaid += paymentRequest.Amount;
                        
                        // Update invoice if payment is immediately cleared
                        if (paymentStatus == PaymentStatus.CLEARED)
                        {
                            sale.PaidAmount += paymentRequest.Amount;
                            sale.LastPaymentDate = payment.PaymentDate;
                        }
                    }

                    // Update payment status based on total paid
                    if (totalPaid >= grandTotal)
                    {
                        sale.PaymentStatus = SalePaymentStatus.Paid;
                    }
                    else if (totalPaid > 0)
                    {
                        sale.PaymentStatus = SalePaymentStatus.Partial;
                    }
                    else
                    {
                        sale.PaymentStatus = SalePaymentStatus.Pending;
                    }

                    // Update customer balance if any payments are cleared
                    if (request.CustomerId.HasValue)
                    {
                        var clearedAmount = request.Payments
                            .Where(p => p.Method.ToUpper() == "CASH" || p.Method.ToUpper() == "ONLINE")
                            .Sum(p => p.Amount);
                        
                        if (clearedAmount > 0)
                        {
                            var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                            if (customer != null)
                            {
                                customer.Balance -= clearedAmount;
                                customer.LastActivity = DateTime.UtcNow;
                                customer.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }
                else
                {
                    // No payment provided - mark as pending
                    sale.PaymentStatus = SalePaymentStatus.Pending;
                    sale.PaidAmount = 0;
                    
                    // New sale increases customer balance (customer owes more)
                    if (request.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance += grandTotal;
                            customer.LastActivity = DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Sale Created",
                    Details = $"Invoice: {invoiceNo}, Total: {grandTotal:C}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ✅ REAL-TIME BALANCE UPDATE: Update customer balance after invoice creation
                if (request.CustomerId.HasValue)
                {
                    try
                    {
                        await _balanceService.UpdateCustomerBalanceOnInvoiceCreatedAsync(
                            request.CustomerId.Value,
                            grandTotal);
                        
                        // Also update for any cleared payments
                        if (request.Payments != null && request.Payments.Any())
                        {
                            var clearedAmount = request.Payments
                                .Where(p => p.Method.ToUpper() == "CASH" || p.Method.ToUpper() == "ONLINE")
                                .Sum(p => p.Amount);
                            
                            if (clearedAmount > 0)
                            {
                                await _balanceService.UpdateCustomerBalanceOnPaymentCreatedAsync(
                                    request.CustomerId.Value,
                                    clearedAmount);
                            }
                        }
                    }
                    catch (Exception balanceEx)
                    {
                        Console.WriteLine($"⚠️ Failed to update balance: {balanceEx.Message}");
                        // Don't fail the sale, but create alert
                        await _alertService.CreateAlertAsync(
                            AlertType.BalanceMismatch,
                            "Failed to update customer balance after invoice creation",
                            $"Invoice: {invoiceNo}, Customer: {request.CustomerId}",
                            AlertSeverity.Warning);
                    }
                }

                // Auto-backup after successful invoice save
                try
                {
                    var savedSale = await GetSaleByIdAsync(sale.Id);
                    if (savedSale != null)
                    {
                        // Generate and save PDF
                        await _pdfService.GenerateInvoicePdfAsync(savedSale);
                        
                        // Create backup (background task, don't block)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _backupService.CreateFullBackupAsync(exportToDesktop: true);
                                Console.WriteLine("✅ Auto-backup completed to Desktop");
                            }
                            catch (Exception backupEx)
                            {
                                Console.WriteLine($"⚠️ Auto-backup failed: {backupEx.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to auto-backup after invoice save: {ex.Message}");
                    // Don't fail the sale creation if backup fails
                }

                return await GetSaleByIdAsync(sale.Id) ?? throw new InvalidOperationException("Failed to retrieve created sale");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                // Log detailed error for debugging
                Console.WriteLine($"❌ CreateSaleAsync Error: {ex.GetType().Name}");
                Console.WriteLine($"❌ Message: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"❌ Inner Message: {ex.InnerException.Message}");
                    Console.WriteLine($"❌ Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                // Re-throw to be caught by controller
                throw;
            }
        }

        public async Task<SaleDto> CreateSaleWithOverrideAsync(CreateSaleRequest request, string reason, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Similar to CreateSaleAsync but without stock validation
                var invoiceNo = await GenerateInvoiceNumberAsync();
                var vatPercent = await GetVatPercentAsync();
                decimal subtotal = 0;
                decimal vatTotal = 0;

                var saleItems = new List<SaleItem>();
                var inventoryTransactions = new List<InventoryTransaction>();

                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found");

                    var baseQty = item.Qty * product.ConversionToBase;
                    // Calculate line totals: Total = qty × price, VAT = Total × 5%, Amount = Total + VAT
                    var rowTotal = item.UnitPrice * item.Qty;
                    var vatAmount = Math.Round(rowTotal * (vatPercent / 100), 2);
                    var lineAmount = rowTotal + vatAmount;

                    subtotal += rowTotal;
                    vatTotal += vatAmount;

                    var saleItem = new SaleItem
                    {
                        ProductId = item.ProductId,
                        UnitType = item.UnitType,
                        Qty = item.Qty,
                        UnitPrice = item.UnitPrice,
                        Discount = 0, // No per-item discount
                        VatAmount = vatAmount,
                        LineTotal = lineAmount
                    };

                    saleItems.Add(saleItem);

                    // Update stock even if negative (admin override)
                    product.StockQty -= baseQty;
                    product.UpdatedAt = DateTime.UtcNow;

                    var inventoryTransaction = new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        ChangeQty = -baseQty,
                        TransactionType = TransactionType.Sale,
                        RefId = null,
                        Reason = $"Admin Override: {reason}",
                        CreatedAt = DateTime.UtcNow
                    };

                    inventoryTransactions.Add(inventoryTransaction);
                }

                // Apply global discount (subtract from subtotal + VAT)
                var grandTotal = Math.Round((subtotal + vatTotal - request.Discount), 2);

                // CASH CUSTOMER LOGIC: If no customer ID (cash customer), auto-mark as paid with cash payment
                bool isCashCustomerOverride = !request.CustomerId.HasValue;
                decimal initialPaidAmountOverride = 0;
                SalePaymentStatus initialPaymentStatusOverride = SalePaymentStatus.Pending;
                
                if (isCashCustomerOverride)
                {
                    // Cash customer = instant payment, mark as paid immediately
                    initialPaidAmountOverride = grandTotal;
                    initialPaymentStatusOverride = SalePaymentStatus.Paid;
                }

                var sale = new Sale
                {
                    InvoiceNo = invoiceNo,
                    InvoiceDate = DateTime.UtcNow,
                    CustomerId = request.CustomerId,
                    Subtotal = subtotal,
                    VatTotal = vatTotal,
                    Discount = request.Discount,
                    GrandTotal = grandTotal,
                    TotalAmount = grandTotal, // Set TotalAmount = GrandTotal
                    PaidAmount = initialPaidAmountOverride, // Cash customer = paid immediately
                    PaymentStatus = initialPaymentStatusOverride, // Cash customer = Paid status
                    IsFinalized = true, // Invoice is finalized - stock decremented
                    Notes = request.Notes,
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Sales.Add(sale);
                await _context.SaveChangesAsync();

                foreach (var item in saleItems)
                {
                    item.SaleId = sale.Id;
                }

                _context.SaleItems.AddRange(saleItems);

                foreach (var invTx in inventoryTransactions)
                {
                    invTx.RefId = sale.Id;
                }

                _context.InventoryTransactions.AddRange(inventoryTransactions);

                // Process payments
                decimal totalPaid = 0;
                
                // CASH CUSTOMER: Auto-create cash payment if no customer ID
                if (isCashCustomerOverride)
                {
                    // Create automatic cash payment for cash customer
                    var cashPayment = new Payment
                    {
                        SaleId = sale.Id,
                        CustomerId = null, // Cash customer has no customer ID
                        Amount = grandTotal,
                        Mode = PaymentMode.CASH,
                        Reference = "CASH",
                        Status = PaymentStatus.CLEARED, // Cash is always cleared
                        PaymentDate = DateTime.UtcNow,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.Payments.Add(cashPayment);
                    totalPaid = grandTotal;
                }
                else if (request.Payments != null && request.Payments.Any())
                {
                    var payments = request.Payments.Select(p => {
                        var paymentMode = Enum.Parse<PaymentMode>(p.Method.ToUpper());
                        var paymentStatus = paymentMode == PaymentMode.CHEQUE 
                            ? PaymentStatus.PENDING 
                            : (paymentMode == PaymentMode.CASH || paymentMode == PaymentMode.ONLINE 
                                ? PaymentStatus.CLEARED 
                                : PaymentStatus.PENDING);
                        
                        return new Payment
                        {
                            SaleId = sale.Id,
                            CustomerId = request.CustomerId,
                            Amount = p.Amount,
                            Mode = paymentMode,
                            Reference = p.Ref,
                            Status = paymentStatus,
                            PaymentDate = DateTime.UtcNow,
                            CreatedBy = userId,
                            CreatedAt = DateTime.UtcNow
                        };
                    }).ToList();

                    _context.Payments.AddRange(payments);

                    totalPaid = payments.Sum(p => p.Amount);
                    if (totalPaid >= grandTotal)
                    {
                        sale.PaymentStatus = SalePaymentStatus.Paid;
                    }
                    else if (totalPaid > 0)
                    {
                        sale.PaymentStatus = SalePaymentStatus.Partial;
                    }
                }

                // Recalculate customer balance from all transactions (fixes fake balance issue)
                if (request.CustomerId.HasValue)
                {
                    var customerService = new CustomerService(_context);
                    await customerService.RecalculateCustomerBalanceAsync(request.CustomerId.Value);
                }

                // Create audit log for override
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Sale Created (Admin Override)",
                    Details = $"Invoice: {invoiceNo}, Total: {grandTotal:C}, Reason: {reason}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Auto-backup after successful invoice save
                try
                {
                    var savedSale = await GetSaleByIdAsync(sale.Id);
                    if (savedSale != null)
                    {
                        // Generate and save PDF
                        await _pdfService.GenerateInvoicePdfAsync(savedSale);
                        
                        // Create backup (background task, don't block)
                        _ = Task.Run(async () =>
                        {
                            try
                            {
                                await _backupService.CreateFullBackupAsync(exportToDesktop: true);
                                Console.WriteLine("✅ Auto-backup completed to Desktop");
                            }
                            catch (Exception backupEx)
                            {
                                Console.WriteLine($"⚠️ Auto-backup failed: {backupEx.Message}");
                            }
                        });
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"⚠️ Failed to auto-backup after invoice save: {ex.Message}");
                    // Don't fail the sale creation if backup fails
                }

                return await GetSaleByIdAsync(sale.Id) ?? throw new InvalidOperationException("Failed to retrieve created sale");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<string> GenerateInvoiceNumberAsync()
        {
            // Delegate to InvoiceNumberService for consistency
            return await _invoiceNumberService.GenerateNextInvoiceNumberAsync();
        }

        public async Task<SaleDto> UpdateSaleAsync(int saleId, CreateSaleRequest request, int userId, string? editReason = null, byte[]? expectedRowVersion = null)
        {
            // Use serializable isolation level to prevent concurrent edits
            using var transaction = await _context.Database.BeginTransactionAsync(System.Data.IsolationLevel.Serializable);
            try
            {
                // Get existing sale with items - use pessimistic lock to prevent concurrent access
                var existingSale = await _context.Sales
                    .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                    .AsNoTracking() // First read to check version
                    .FirstOrDefaultAsync(s => s.Id == saleId);

                if (existingSale == null)
                    throw new InvalidOperationException("Sale not found");

                if (existingSale.IsDeleted)
                    throw new InvalidOperationException("Cannot edit deleted sale");

                // Verify user exists and has permission (Admin OR Staff can edit)
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    throw new InvalidOperationException("User not found");
                
                // Allow both Admin and Staff to edit invoices (no 48-hour lock)
                if (user.Role != UserRole.Admin && user.Role != UserRole.Staff)
                {
                    throw new InvalidOperationException("Only Admin and Staff users can edit invoices");
                }

                // CONCURRENCY CHECK: Verify version hasn't changed (another user edited it)
                if (expectedRowVersion != null && expectedRowVersion.Length > 0)
                {
                    var currentSale = await _context.Sales.FindAsync(saleId);
                    if (currentSale == null)
                        throw new InvalidOperationException("Sale not found");

                    // Compare RowVersion bytes to detect concurrent modification
                    if (currentSale.RowVersion != null && currentSale.RowVersion.Length > 0)
                    {
                        if (!currentSale.RowVersion.SequenceEqual(expectedRowVersion))
                        {
                            await transaction.RollbackAsync();
                            throw new InvalidOperationException(
                                $"CONFLICT: This invoice was modified by another user. " +
                                $"Current version: {currentSale.Version}. " +
                                $"Last modified by: {currentSale.LastModifiedByUser?.Name ?? "Unknown"} at {currentSale.LastModifiedAt:g}. " +
                                $"Please refresh and try again."
                            );
                        }
                    }
                }

                // Check if another user is currently editing (check LastModifiedAt within last 30 seconds)
                var recentlyModified = existingSale.LastModifiedAt.HasValue && 
                    (DateTime.UtcNow - existingSale.LastModifiedAt.Value).TotalSeconds < 30 &&
                    existingSale.LastModifiedBy != userId;

                if (recentlyModified)
                {
                    var modifier = await _context.Users.FindAsync(existingSale.LastModifiedBy);
                    throw new InvalidOperationException(
                        $"WARNING: Another user ({modifier?.Name ?? "Unknown"}) is currently editing this invoice. " +
                        $"Please wait a few seconds and refresh before saving."
                    );
                }

                // Now load with tracking for updates
                var saleForUpdate = await _context.Sales
                    .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(s => s.Id == saleId);

                if (saleForUpdate == null)
                    throw new InvalidOperationException("Sale not found");

                // Create version snapshot before editing
                var versionSnapshot = new
                {
                    Sale = new
                    {
                        saleForUpdate.Id,
                        saleForUpdate.InvoiceNo,
                        saleForUpdate.InvoiceDate,
                        saleForUpdate.CustomerId,
                        saleForUpdate.Subtotal,
                        saleForUpdate.VatTotal,
                        saleForUpdate.Discount,
                        saleForUpdate.GrandTotal,
                        saleForUpdate.PaymentStatus,
                        saleForUpdate.Version
                    },
                    Items = saleForUpdate.Items.Select(i => new
                    {
                        i.Id,
                        i.ProductId,
                        i.UnitType,
                        i.Qty,
                        i.UnitPrice,
                        i.Discount,
                        i.VatAmount,
                        i.LineTotal
                    }).ToList()
                };

                var versionJson = JsonSerializer.Serialize(versionSnapshot);
                var newVersion = saleForUpdate.Version + 1;

                // Use validation service for robust validation
                var validationResult = await _validationService.ValidateSaleEditAsync(saleId, request.Items);

                if (!validationResult.IsValid)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException(
                        "VALIDATION FAILED:\n" +
                        string.Join("\n", validationResult.Errors) +
                        "\n\nPlease correct the errors and try again."
                    );
                }

                // STOCK CONFLICT PREVENTION: Check all products have sufficient stock BEFORE making changes
                var stockConflicts = new List<string>();
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                    {
                        stockConflicts.Add($"Product ID {item.ProductId} not found");
                        continue;
                    }

                    var baseQty = item.Qty * product.ConversionToBase;
                    
                    // Calculate current available stock (after restoring old quantities)
                    var oldItem = saleForUpdate.Items?.FirstOrDefault(i => i != null && i.ProductId == item.ProductId);
                    var oldBaseQty = (oldItem != null && product != null) ? oldItem.Qty * product.ConversionToBase : 0;
                    var availableAfterRestore = product.StockQty + oldBaseQty;

                    if (availableAfterRestore < baseQty)
                    {
                        stockConflicts.Add(
                            $"{product.NameEn}: Available: {availableAfterRestore}, Required: {baseQty}"
                        );
                    }
                }

                if (stockConflicts.Any())
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException(
                        "STOCK CONFLICT: Insufficient stock for the following products:\n" +
                        string.Join("\n", stockConflicts) +
                        "\n\nPlease check current stock levels and adjust quantities."
                    );
                }

                // REVERSE OLD TRANSACTIONS: Restore stock and reverse inventory transactions
                if (saleForUpdate.Items != null && saleForUpdate.Items.Any())
                {
                    foreach (var oldItem in saleForUpdate.Items)
                    {
                        if (oldItem != null)
                        {
                            var product = await _context.Products.FindAsync(oldItem.ProductId);
                            if (product != null)
                            {
                                // Restore stock
                                var oldBaseQty = oldItem.Qty * product.ConversionToBase;
                                product.StockQty += oldBaseQty;
                                product.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                }

                // REVERSE customer balance will be recalculated after new amounts are set

                // Calculate new totals
                var vatPercent = await GetVatPercentAsync();
                decimal subtotal = 0;
                decimal vatTotal = 0;

                var newSaleItems = new List<SaleItem>();
                var inventoryTransactions = new List<InventoryTransaction>();

                // Additional validation using validation service (already validated above, but double-check)
                var additionalValidationErrors = new List<string>();
                foreach (var item in request.Items)
                {
                    var qtyResult = await _validationService.ValidateQuantityAsync(item.Qty);
                    if (!qtyResult.IsValid)
                    {
                        additionalValidationErrors.AddRange(qtyResult.Errors.Select(e => $"Item {item.ProductId}: {e}"));
                    }

                    var priceResult = await _validationService.ValidatePriceAsync(item.UnitPrice);
                    if (!priceResult.IsValid)
                    {
                        additionalValidationErrors.AddRange(priceResult.Errors.Select(e => $"Item {item.ProductId}: {e}"));
                    }
                }

                if (additionalValidationErrors.Any())
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException(string.Join("\n", additionalValidationErrors));
                }

                // Process new items (same logic as CreateSaleAsync)
                // Note: Stock has already been restored from old items above
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FirstOrDefaultAsync(p => p.Id == item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found");

                    var baseQty = item.Qty * product.ConversionToBase;

                    // Check stock availability (stock was already restored from old items)
                    // But we need to account for items already processed in this loop
                    // Find if this product appears multiple times in the request
                    var qtyAlreadyProcessed = newSaleItems
                        .Where(si => si.ProductId == item.ProductId)
                        .Sum(si => si.Qty * product.ConversionToBase);
                    
                    var availableStock = product.StockQty - qtyAlreadyProcessed;
                    
                    if (availableStock < baseQty)
                    {
                        throw new InvalidOperationException(
                            $"Insufficient stock for {product.NameEn}. " +
                            $"Available: {availableStock}, Required: {baseQty}. " +
                            $"Note: Stock from old invoice items has been restored."
                        );
                    }

                    var rowTotal = item.UnitPrice * item.Qty;
                    var vatAmount = Math.Round(rowTotal * (vatPercent / 100), 2);
                    var lineAmount = rowTotal + vatAmount;

                    subtotal += rowTotal;
                    vatTotal += vatAmount;

                    var saleItem = new SaleItem
                    {
                        SaleId = saleId,
                        ProductId = item.ProductId,
                        UnitType = string.IsNullOrWhiteSpace(item.UnitType) ? "CRTN" : item.UnitType.ToUpper(),
                        Qty = item.Qty,
                        UnitPrice = item.UnitPrice,
                        Discount = 0,
                        VatAmount = vatAmount,
                        LineTotal = lineAmount
                    };
                    newSaleItems.Add(saleItem);

                    // CRITICAL: Update stock for edited invoice
                    // Old stock was already restored above (line 898-900)
                    // Now decrement stock for new quantities (delta calculation)
                    product.StockQty -= baseQty;
                    product.UpdatedAt = DateTime.UtcNow;

                    // Create inventory transaction
                    inventoryTransactions.Add(new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        ChangeQty = -baseQty,
                        TransactionType = TransactionType.Sale,
                        RefId = saleId,
                        Reason = $"Sale Updated: {existingSale.InvoiceNo}",
                        CreatedAt = DateTime.UtcNow
                    });
                }

                var grandTotal = Math.Round((subtotal + vatTotal - request.Discount), 2);

                // Delete old sale items
                if (saleForUpdate.Items != null && saleForUpdate.Items.Any())
                {
                    _context.SaleItems.RemoveRange(saleForUpdate.Items);
                }

                // Update sale (PRESERVE InvoiceNo - do not change it on edit)
                // InvoiceNo is set once during creation and should never change
                saleForUpdate.Subtotal = subtotal;
                saleForUpdate.VatTotal = vatTotal;
                saleForUpdate.Discount = request.Discount;
                saleForUpdate.GrandTotal = grandTotal;
                saleForUpdate.TotalAmount = grandTotal; // Update TotalAmount
                
                // Admin and Staff: Update invoice date if provided
                if (request.InvoiceDate.HasValue)
                {
                    saleForUpdate.InvoiceDate = request.InvoiceDate.Value;
                }
                
                // Handle paid_amount adjustment if new total is less than paid amount
                if (grandTotal < saleForUpdate.PaidAmount)
                {
                    // Customer has overpaid - adjust balance and paid_amount
                    var excessAmount = saleForUpdate.PaidAmount - grandTotal;
                    saleForUpdate.PaidAmount = grandTotal; // Cap paid amount at new total
                    
                    // Update customer balance (reduce what customer owes)
                    if (saleForUpdate.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(saleForUpdate.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance -= excessAmount; // Customer owes less (credit)
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                    
                    // Create audit log for adjustment
                    var adjustmentLog = new AuditLog
                    {
                        UserId = userId,
                        Action = "Invoice Edit - Paid Amount Adjustment",
                        Details = $"Invoice {saleForUpdate.InvoiceNo}: GrandTotal reduced from {saleForUpdate.GrandTotal + excessAmount:C} to {grandTotal:C}. Excess payment of {excessAmount:C} credited to customer balance.",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.AuditLogs.Add(adjustmentLog);
                }
                
                saleForUpdate.Notes = request.Notes;
                saleForUpdate.LastModifiedBy = userId;
                saleForUpdate.LastModifiedAt = DateTime.UtcNow;
                saleForUpdate.Version = newVersion;
                saleForUpdate.EditReason = editReason;
                // InvoiceNo is NOT updated - preserve original invoice number
                // Update CustomerId if changed - validate customer exists
                if (request.CustomerId.HasValue)
                {
                    var newCustomer = await _context.Customers.FindAsync(request.CustomerId.Value);
                    if (newCustomer == null)
                    {
                        throw new InvalidOperationException($"Customer with ID {request.CustomerId.Value} not found");
                    }
                    
                    // If customer changed, update customer balances
                    if (saleForUpdate.CustomerId != request.CustomerId.Value)
                    {
                        // Remove balance from old customer
                        if (saleForUpdate.CustomerId.HasValue)
                        {
                            var oldCustomer = await _context.Customers.FindAsync(saleForUpdate.CustomerId.Value);
                            if (oldCustomer != null)
                            {
                                oldCustomer.Balance -= (saleForUpdate.GrandTotal - saleForUpdate.PaidAmount);
                                oldCustomer.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                        
                        // Add balance to new customer
                        newCustomer.Balance += (grandTotal - saleForUpdate.PaidAmount);
                        newCustomer.UpdatedAt = DateTime.UtcNow;
                    }
                    else
                    {
                        // Same customer - adjust balance based on grand total change
                        var balanceChange = grandTotal - saleForUpdate.GrandTotal;
                        newCustomer.Balance += balanceChange;
                        newCustomer.UpdatedAt = DateTime.UtcNow;
                    }
                    
                    saleForUpdate.CustomerId = request.CustomerId.Value;
                }
                else if (saleForUpdate.CustomerId.HasValue)
                {
                    // Customer removed - adjust old customer balance
                    var oldCustomer = await _context.Customers.FindAsync(saleForUpdate.CustomerId.Value);
                    if (oldCustomer != null)
                    {
                        oldCustomer.Balance -= (saleForUpdate.GrandTotal - saleForUpdate.PaidAmount);
                        oldCustomer.UpdatedAt = DateTime.UtcNow;
                    }
                    saleForUpdate.CustomerId = null;
                }

                // Add new sale items
                _context.SaleItems.AddRange(newSaleItems);

                // Add inventory transactions
                _context.InventoryTransactions.AddRange(inventoryTransactions);

                // Process payments if provided
                if (request.Payments != null && request.Payments.Any())
                {
                    // Delete old payments for this sale
                    var oldPayments = await _context.Payments.Where(p => p.SaleId == saleId).ToListAsync();
                    if (oldPayments != null && oldPayments.Any())
                    {
                        _context.Payments.RemoveRange(oldPayments);
                    }

                    // Add new payments using EF Core (PostgreSQL compatible - NO raw SQL)
                    foreach (var p in request.Payments)
                    {
                        if (string.IsNullOrWhiteSpace(p.Method))
                        {
                            throw new InvalidOperationException("Payment method cannot be empty");
                        }
                        
                        // Try to parse payment mode, handle invalid values
                        PaymentMode paymentMode;
                        try
                        {
                            paymentMode = Enum.Parse<PaymentMode>(p.Method.ToUpper());
                        }
                        catch (ArgumentException)
                        {
                            throw new InvalidOperationException($"Invalid payment method: {p.Method}. Valid methods are: Cash, Cheque, Online, Credit");
                        }
                        
                        var paymentStatus = paymentMode == PaymentMode.CHEQUE 
                            ? PaymentStatus.PENDING 
                            : (paymentMode == PaymentMode.CASH || paymentMode == PaymentMode.ONLINE 
                                ? PaymentStatus.CLEARED 
                                : PaymentStatus.PENDING);
                        
                        if (p.Amount <= 0)
                        {
                            throw new InvalidOperationException($"Payment amount must be greater than 0. Received: {p.Amount}");
                        }
                        
                        var paymentDate = DateTime.UtcNow;
                        
                        // Create payment using EF Core (works with PostgreSQL)
                        var payment = new Payment
                        {
                            SaleId = saleId,
                            CustomerId = request.CustomerId,
                            Amount = p.Amount,
                            Mode = paymentMode,
                            Reference = p.Ref,
                            Status = paymentStatus,
                            PaymentDate = paymentDate,
                            CreatedBy = userId,
                            CreatedAt = paymentDate,
                            UpdatedAt = paymentDate,
                            RowVersion = new byte[0]
                        };
                        
                        _context.Payments.Add(payment);
                    }
                    
                    // Save all payments at once
                    await _context.SaveChangesAsync();

                    // Calculate total paid from request payments
                    var totalPaid = request.Payments.Sum(p => p.Amount);
                    if (totalPaid >= grandTotal)
                    {
                        saleForUpdate.PaymentStatus = SalePaymentStatus.Paid;
                    }
                    else if (totalPaid > 0)
                    {
                        saleForUpdate.PaymentStatus = SalePaymentStatus.Partial;
                    }
                    else
                    {
                        saleForUpdate.PaymentStatus = SalePaymentStatus.Pending;
                    }
                    
                    // Update PaidAmount to match total paid
                    saleForUpdate.PaidAmount = totalPaid;
                }
                else
                {
                    // No payments provided - reset payment status
                    saleForUpdate.PaymentStatus = SalePaymentStatus.Pending;
                    saleForUpdate.PaidAmount = 0;
                }

                // Recalculate customer balance with new amount
                if (request.CustomerId.HasValue)
                {
                    var customerService = new CustomerService(_context);
                    await customerService.RecalculateCustomerBalanceAsync(request.CustomerId.Value);
                    
                    // Update customer LastActivity
                    var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                    if (customer != null)
                    {
                        customer.LastActivity = DateTime.UtcNow;
                        customer.UpdatedAt = DateTime.UtcNow;
                        await _context.SaveChangesAsync();
                    }
                }

                // Calculate diff summary for better audit trail
                var diffSummary = CalculateInvoiceDiff(versionSnapshot, request, saleForUpdate, user.Name, newVersion);
                
                // Save InvoiceVersion snapshot
                var invoiceVersion = new InvoiceVersion
                {
                    SaleId = saleId,
                    VersionNumber = saleForUpdate.Version - 1, // Previous version
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    DataJson = versionJson,
                    EditReason = editReason,
                    DiffSummary = diffSummary // Enhanced diff summary
                };
                _context.InvoiceVersions.Add(invoiceVersion);

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Sale Updated",
                    Details = $"Invoice: {saleForUpdate.InvoiceNo} updated to Version {newVersion}. Reason: {editReason ?? "N/A"}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);

                // Save changes - this will also update RowVersion automatically
                await _context.SaveChangesAsync();
                
                // Verify no concurrent modification occurred during save
                await _context.Entry(saleForUpdate).ReloadAsync();
                if (saleForUpdate.Version != newVersion)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException(
                        "CONFLICT DETECTED: Invoice was modified during your edit. " +
                        $"Please refresh and try again. Current version: {saleForUpdate.Version}"
                    );
                }

                await transaction.CommitAsync();

                return await GetSaleByIdAsync(saleId) ?? throw new InvalidOperationException("Failed to retrieve updated sale");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                
                // Log detailed error for debugging
                Console.WriteLine($"❌ UpdateSaleAsync Error for SaleId {saleId}:");
                Console.WriteLine($"❌ Error Type: {ex.GetType().Name}");
                Console.WriteLine($"❌ Message: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.GetType().Name}");
                    Console.WriteLine($"❌ Inner Message: {ex.InnerException.Message}");
                    Console.WriteLine($"❌ Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                
                // Re-throw to be caught by controller
                throw;
            }
        }

        public async Task<bool> DeleteSaleAsync(int saleId, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var sale = await _context.Sales
                    .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(s => s.Id == saleId);

                if (sale == null)
                    return false;

                if (sale.IsDeleted)
                    return true; // Already deleted

                // REVERSE TRANSACTIONS: Restore stock when invoice is canceled/deleted
                // Only restore if invoice was finalized (stock was decremented)
                if (sale.IsFinalized)
                {
                    foreach (var item in sale.Items)
                    {
                        var product = await _context.Products.FindAsync(item.ProductId);
                        if (product != null)
                        {
                            var baseQty = item.Qty * product.ConversionToBase;
                            product.StockQty += baseQty;
                            product.UpdatedAt = DateTime.UtcNow;

                            // Create reversal transaction
                            _context.InventoryTransactions.Add(new InventoryTransaction
                            {
                                ProductId = product.Id,
                                ChangeQty = baseQty,
                                TransactionType = TransactionType.Adjustment,
                                Reason = $"Sale Deleted/Canceled: {sale.InvoiceNo}",
                                CreatedAt = DateTime.UtcNow
                            });
                        }
                    }
                }

                // CRITICAL: Delete or void all related payments
                var relatedPayments = await _context.Payments
                    .Where(p => p.SaleId == saleId)
                    .ToListAsync();

                foreach (var payment in relatedPayments)
                {
                    // If payment was cleared, reverse its effects before deletion
                    if (payment.Status == PaymentStatus.CLEARED)
                    {
                        // Reverse customer balance adjustment
                        if (payment.CustomerId.HasValue)
                        {
                            var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                            if (customer != null)
                            {
                                customer.Balance += payment.Amount; // Reverse: customer owes more
                                customer.UpdatedAt = DateTime.UtcNow;
                            }
                        }
                    }
                    
                    // Delete payment record
                    _context.Payments.Remove(payment);
                }

                // CRITICAL: Reset sale payment status and amounts
                sale.PaidAmount = 0;
                sale.PaymentStatus = SalePaymentStatus.Pending;
                sale.LastPaymentDate = null;

                // Recalculate customer balance (after payment deletion)
                if (sale.CustomerId.HasValue)
                {
                    var customerService = new CustomerService(_context);
                    await customerService.RecalculateCustomerBalanceAsync(sale.CustomerId.Value);
                }

                // Soft delete
                sale.IsDeleted = true;
                sale.DeletedBy = userId;
                sale.DeletedAt = DateTime.UtcNow;

                // Create audit log
                var user = await _context.Users.FindAsync(userId);
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Sale Deleted",
                    Details = $"Invoice: {sale.InvoiceNo}, Total: {sale.GrandTotal:C}, Customer: {sale.Customer?.Name ?? "Cash"}, Deleted by: {user?.Name ?? "Unknown"}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);

                // Create alert for invoice deletion (for admin tracking)
                await _alertService.CreateAlertAsync(
                    AlertType.InvoiceDeleted,
                    $"Invoice {sale.InvoiceNo} deleted",
                    $"Deleted by {user?.Name ?? "Unknown"}. Total: {sale.GrandTotal:C}, Customer: {sale.Customer?.Name ?? "Cash"}",
                    AlertSeverity.Warning,
                    new Dictionary<string, object> {
                        { "InvoiceNo", sale.InvoiceNo },
                        { "InvoiceId", sale.Id },
                        { "GrandTotal", sale.GrandTotal },
                        { "CustomerId", sale.CustomerId ?? 0 },
                        { "DeletedBy", user?.Name ?? "Unknown" }
                    }
                );

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // ✅ REAL-TIME BALANCE UPDATE: Update customer balance after invoice deletion
                if (sale.CustomerId.HasValue)
                {
                    try
                    {
                        await _balanceService.UpdateCustomerBalanceOnInvoiceDeletedAsync(
                            sale.CustomerId.Value,
                            sale.GrandTotal);
                        
                        // Reverse any cleared payments
                        foreach (var payment in relatedPayments.Where(p => p.Status == PaymentStatus.CLEARED))
                        {
                            await _balanceService.UpdateCustomerBalanceOnPaymentDeletedAsync(
                                sale.CustomerId.Value,
                                payment.Amount);
                        }
                    }
                    catch (Exception balanceEx)
                    {
                        Console.WriteLine($"⚠️ Failed to update balance after deletion: {balanceEx.Message}");
                        // Create alert for admin
                        await _alertService.CreateAlertAsync(
                            AlertType.BalanceMismatch,
                            "Failed to update customer balance after invoice deletion",
                            $"Invoice: {sale.InvoiceNo}, Customer: {sale.CustomerId}",
                            AlertSeverity.Warning);
                    }
                }

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<byte[]> GenerateInvoicePdfAsync(int saleId)
        {
            try
            {
                Console.WriteLine($"\n📄 PDF Generation: Starting for sale {saleId}");
                
                var sale = await _context.Sales
                    .Include(s => s.Customer)
                    .Include(s => s.Items)
                        .ThenInclude(i => i.Product)
                    .FirstOrDefaultAsync(s => s.Id == saleId);

                if (sale == null)
                {
                    Console.WriteLine($"❌ PDF Generation: Sale with ID {saleId} not found");
                    throw new InvalidOperationException($"Sale with ID {saleId} not found");
                }
                
                Console.WriteLine($"✅ PDF Generation: Sale {saleId} found - {sale.Items?.Count ?? 0} items");
                
                var saleDto = new SaleDto
                {
                    Id = sale.Id,
                    InvoiceNo = sale.InvoiceNo ?? $"INV-{saleId}",
                    InvoiceDate = sale.InvoiceDate,
                    CustomerId = sale.CustomerId,
                    CustomerName = sale.Customer?.Name ?? "Cash Customer",
                    Subtotal = sale.Subtotal,
                    VatTotal = sale.VatTotal,
                    GrandTotal = sale.GrandTotal,
                    PaymentStatus = sale.PaymentStatus.ToString(),
                    PaidAmount = sale.PaidAmount,
                    Items = sale.Items?.Select(i => new SaleItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product?.NameEn ?? $"Product {i.ProductId}",
                        Qty = i.Qty,
                        UnitPrice = i.UnitPrice,
                        UnitType = i.Product?.UnitType ?? "CRTN",
                        LineTotal = i.LineTotal,
                        VatAmount = i.VatAmount
                    }).ToList() ?? new List<SaleItemDto>()
                };

                Console.WriteLine($"✅ PDF Generation: Calling PdfService...");
                var pdfBytes = await _pdfService.GenerateInvoicePdfAsync(saleDto);
                
                Console.WriteLine($"✅ PDF Generation: SUCCESS! Generated {pdfBytes?.Length ?? 0} bytes");
                return pdfBytes;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ PDF Error: {ex.Message}");
                throw;
            }
        }

        public async Task<bool> CanEditInvoiceAsync(int saleId, int userId, string userRole)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null || sale.IsDeleted)
                return false;

            // Only Admin can edit invoices
            return userRole == "Admin";
        }

        public async Task<bool> UnlockInvoiceAsync(int saleId, int userId, string unlockReason)
        {
            var sale = await _context.Sales.FindAsync(saleId);
            if (sale == null)
                return false;

            sale.IsLocked = false;
            sale.LockedAt = null;

            // Create audit log
            var auditLog = new AuditLog
            {
                UserId = userId,
                Action = "Invoice Unlocked",
                Details = $"Invoice: {sale.InvoiceNo} unlocked. Reason: {unlockReason}",
                CreatedAt = DateTime.UtcNow
            };
            _context.AuditLogs.Add(auditLog);

            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<List<InvoiceVersion>> GetInvoiceVersionsAsync(int saleId)
        {
            return await _context.InvoiceVersions
                .Include(v => v.CreatedByUser)
                .Where(v => v.SaleId == saleId)
                .OrderByDescending(v => v.VersionNumber)
                .ToListAsync();
        }

        public async Task<bool> LockOldInvoicesAsync()
        {
            // Lock invoices created more than 48 hours ago
            var cutoffTime = DateTime.UtcNow.AddHours(-48);
            var invoicesToLock = await _context.Sales
                .Where(s => !s.IsLocked && !s.IsDeleted && s.CreatedAt < cutoffTime)
                .ToListAsync();

            foreach (var invoice in invoicesToLock)
            {
                invoice.IsLocked = true;
                invoice.LockedAt = DateTime.UtcNow;
            }

            if (invoicesToLock.Any())
            {
                await _context.SaveChangesAsync();
                return true;
            }

            return false;
        }

        private string CalculateInvoiceDiff(dynamic oldVersion, CreateSaleRequest newRequest, Sale oldSale, string editorName, int newVersion)
        {
            var changes = new List<string>();
            
            // Compare totals
            var oldTotal = oldSale.GrandTotal;
            var newTotal = (newRequest.Items?.Sum(i => i.UnitPrice * i.Qty) ?? 0m) * 1.05m - newRequest.Discount;
            if (Math.Abs(oldTotal - newTotal) > 0.01m)
            {
                changes.Add($"GrandTotal: {oldTotal:C} → {newTotal:C}");
            }
            
            // Compare discount
            if (Math.Abs((oldSale.Discount) - newRequest.Discount) > 0.01m)
            {
                changes.Add($"Discount: {oldSale.Discount:C} → {newRequest.Discount:C}");
            }
            
            // Compare item counts
            var oldItemCount = oldSale.Items?.Count ?? 0;
            var newItemCount = newRequest.Items?.Count ?? 0;
            if (oldItemCount != newItemCount)
            {
                changes.Add($"Items: {oldItemCount} → {newItemCount}");
            }
            
            // Compare customer
            if (oldSale.CustomerId != newRequest.CustomerId)
            {
                changes.Add($"Customer changed");
            }
            
            var summary = changes.Any() 
                ? $"Edited by {editorName} - Version {newVersion}. Changes: {string.Join(", ", changes)}"
                : $"Edited by {editorName} - Version {newVersion}";
            
            return summary;
        }

        public async Task<SaleDto?> RestoreInvoiceVersionAsync(int saleId, int versionNumber, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get the version to restore
                var version = await _context.InvoiceVersions
                    .FirstOrDefaultAsync(v => v.SaleId == saleId && v.VersionNumber == versionNumber);
                
                if (version == null)
                    throw new InvalidOperationException($"Version {versionNumber} not found for invoice {saleId}");
                
                // Deserialize the old version data
                var oldData = JsonSerializer.Deserialize<dynamic>(version.DataJson);
                if (oldData == null)
                    throw new InvalidOperationException("Failed to deserialize version data");
                
                // Get current sale
                var currentSale = await _context.Sales
                    .Include(s => s.Items)
                    .FirstOrDefaultAsync(s => s.Id == saleId);
                
                if (currentSale == null)
                    throw new InvalidOperationException("Sale not found");
                
                // Create version snapshot of current state before restore
                var currentSnapshot = new
                {
                    Sale = new
                    {
                        currentSale.Id,
                        currentSale.InvoiceNo,
                        currentSale.InvoiceDate,
                        currentSale.CustomerId,
                        currentSale.Subtotal,
                        currentSale.VatTotal,
                        currentSale.Discount,
                        currentSale.GrandTotal,
                        currentSale.PaymentStatus,
                        currentSale.Version
                    },
                    Items = currentSale.Items.Select(i => new
                    {
                        i.Id,
                        i.ProductId,
                        i.UnitType,
                        i.Qty,
                        i.UnitPrice,
                        i.Discount,
                        i.VatAmount,
                        i.LineTotal
                    }).ToList()
                };
                
                var currentVersionJson = JsonSerializer.Serialize(currentSnapshot);
                var newVersion = currentSale.Version + 1;
                
                // Restore old items - reverse current stock changes first
                foreach (var item in currentSale.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        var baseQty = item.Qty * product.ConversionToBase;
                        product.StockQty += baseQty; // Restore stock
                        product.UpdatedAt = DateTime.UtcNow;
                    }
                }
                
                // Delete current items
                _context.SaleItems.RemoveRange(currentSale.Items);
                
                // TODO: Restore old items from version.DataJson
                // This requires deserializing and recreating SaleItems
                // For now, this is a placeholder - full implementation requires parsing the JSON structure
                
                // Save version snapshot
                var restoreVersion = new InvoiceVersion
                {
                    SaleId = saleId,
                    VersionNumber = currentSale.Version,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow,
                    DataJson = currentVersionJson,
                    EditReason = $"Restored to version {versionNumber}",
                    DiffSummary = $"Restored to version {versionNumber} by user {userId}"
                };
                _context.InvoiceVersions.Add(restoreVersion);
                
                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Invoice Version Restored",
                    Details = $"Invoice {currentSale.InvoiceNo} restored to version {versionNumber}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);
                
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                
                return await GetSaleByIdAsync(saleId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<decimal> GetVatPercentAsync()
        {
            var setting = await _context.Settings
                .FirstOrDefaultAsync(s => s.Key == "VAT_PERCENT");
            
            return decimal.TryParse(setting?.Value, out decimal vatPercent) ? vatPercent : 5;
        }

        /// <summary>
        /// Get all deleted sales for audit trail (Admin only)
        /// </summary>
        public async Task<List<SaleDto>> GetDeletedSalesAsync()
        {
            var deletedSales = await _context.Sales
                .Include(s => s.Customer)
                .Include(s => s.Items)
                    .ThenInclude(i => i.Product)
                .Include(s => s.DeletedByUser)
                .Where(s => s.IsDeleted)
                .OrderByDescending(s => s.DeletedAt)
                .Take(100) // Limit to last 100 deleted sales
                .ToListAsync();

            return deletedSales.Select(s => new SaleDto
            {
                Id = s.Id,
                InvoiceNo = s.InvoiceNo,
                InvoiceDate = s.InvoiceDate,
                CustomerId = s.CustomerId,
                CustomerName = s.Customer?.Name,
                Subtotal = s.Subtotal,
                VatTotal = s.VatTotal,
                Discount = s.Discount,
                GrandTotal = s.GrandTotal,
                PaymentStatus = s.PaymentStatus.ToString(),
                Notes = s.Notes,
                CreatedAt = s.CreatedAt,
                DeletedBy = s.DeletedByUser?.Name,
                DeletedAt = s.DeletedAt,
                Items = s.Items.Select(i => new SaleItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product?.NameEn ?? "Unknown",
                    UnitType = i.UnitType,
                    Qty = i.Qty,
                    UnitPrice = i.UnitPrice,
                    Discount = i.Discount,
                    VatAmount = i.VatAmount,
                    LineTotal = i.LineTotal
                }).ToList()
            }).ToList();
        }
    }
}

