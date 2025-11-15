/*
Purpose: Payment service for payment tracking with atomic transactions
Author: AI Assistant
Date: 2024
Updated: 2025 - Complete rewrite per spec for proper payment/invoice/balance tracking
*/
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IPaymentService
    {
        Task<PagedResponse<PaymentDto>> GetPaymentsAsync(int page = 1, int pageSize = 10);
        Task<PaymentDto?> GetPaymentByIdAsync(int id);
        Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, int userId, string? idempotencyKey = null);
        Task<bool> UpdatePaymentStatusAsync(int paymentId, PaymentStatus status, int userId);
        Task<PaymentDto?> UpdatePaymentAsync(int paymentId, UpdatePaymentRequest request, int userId);
        Task<bool> DeletePaymentAsync(int paymentId, int userId);
        Task<List<Models.OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(int customerId);
        Task<InvoiceAmountDto> GetInvoiceAmountAsync(int invoiceId);
        Task<CreatePaymentResponse> AllocatePaymentAsync(AllocatePaymentRequest request, int userId, string? idempotencyKey = null);
    }

    public class PaymentService : IPaymentService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<PaymentService> _logger;
        private readonly IValidationService _validationService;
        private readonly IBalanceService _balanceService;
        private readonly IAlertService _alertService;

        public PaymentService(
            AppDbContext context, 
            ILogger<PaymentService> logger, 
            IValidationService validationService,
            IBalanceService balanceService,
            IAlertService alertService)
        {
            _context = context;
            _logger = logger;
            _validationService = validationService;
            _balanceService = balanceService;
            _alertService = alertService;
        }

        public async Task<PagedResponse<PaymentDto>> GetPaymentsAsync(int page = 1, int pageSize = 10)
        {
            var query = _context.Payments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Include(p => p.CreatedByUser)
                .AsQueryable();

            var totalCount = await query.CountAsync();
            var payments = await query
                .OrderByDescending(p => p.PaymentDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PaymentDto
                {
                    Id = p.Id,
                    SaleId = p.SaleId,
                    InvoiceNo = p.Sale != null ? p.Sale.InvoiceNo : null,
                    CustomerId = p.CustomerId,
                    CustomerName = p.Customer != null ? p.Customer.Name : null,
                    Amount = p.Amount,
                    Mode = p.Mode.ToString(),
                    Reference = p.Reference,
                    Status = p.Status.ToString(),
                    PaymentDate = p.PaymentDate,
                    CreatedBy = p.CreatedBy,
                    CreatedAt = p.CreatedAt
                })
                .ToListAsync();

            return new PagedResponse<PaymentDto>
            {
                Items = payments,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<PaymentDto?> GetPaymentByIdAsync(int id)
        {
            var payment = await _context.Payments
                .Include(p => p.Sale)
                .Include(p => p.Customer)
                .Include(p => p.CreatedByUser)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null) return null;

            return new PaymentDto
            {
                Id = payment.Id,
                SaleId = payment.SaleId,
                InvoiceNo = payment.Sale?.InvoiceNo,
                CustomerId = payment.CustomerId,
                CustomerName = payment.Customer?.Name,
                Amount = payment.Amount,
                Mode = payment.Mode.ToString(),
                Reference = payment.Reference,
                Status = payment.Status.ToString(),
                PaymentDate = payment.PaymentDate,
                CreatedBy = payment.CreatedBy,
                CreatedAt = payment.CreatedAt
            };
        }

        public async Task<CreatePaymentResponse> CreatePaymentAsync(CreatePaymentRequest request, int userId, string? idempotencyKey = null)
        {
            // Validate request
            if (request.Amount <= 0)
                throw new ArgumentException("Payment amount must be greater than zero");

            if (!request.CustomerId.HasValue)
                throw new ArgumentException("Customer ID is required");

            // Check idempotency if key provided
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingRequest = await _context.PaymentIdempotencies
                    .FirstOrDefaultAsync(pr => pr.IdempotencyKey == idempotencyKey);
                
                if (existingRequest != null)
                {
                    // Return existing payment response
                    var existingPayment = await GetPaymentByIdAsync(existingRequest.PaymentId);
                    if (existingPayment != null)
                    {
                        var sale = existingRequest.Payment?.Sale;
                        var customer = existingRequest.Payment?.Customer;
                        
                        return new CreatePaymentResponse
                        {
                            Payment = existingPayment,
                            Invoice = sale != null ? new InvoiceSummaryDto
                            {
                                Id = sale.Id,
                                InvoiceNo = sale.InvoiceNo,
                                TotalAmount = sale.GrandTotal,
                                PaidAmount = sale.PaidAmount,
                                OutstandingAmount = sale.GrandTotal - sale.PaidAmount,
                                Status = sale.PaymentStatus.ToString()
                            } : null,
                            Customer = customer != null ? new CustomerSummaryDto
                            {
                                Id = customer.Id,
                                Name = customer.Name,
                                Balance = customer.Balance
                            } : null
                        };
                    }
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Validate invoice belongs to customer if provided - use FOR UPDATE to lock row
                Sale? invoiceSale = null;
                if (request.SaleId.HasValue)
                {
                    // Re-fetch with RowVersion for optimistic concurrency
                    invoiceSale = await _context.Sales
                        .FirstOrDefaultAsync(s => s.Id == request.SaleId.Value && s.CustomerId == request.CustomerId.Value);
                    
                    if (invoiceSale == null)
                        throw new ArgumentException("Invoice does not belong to the specified customer");

                    // Use validation service for robust validation
                    var validationResult = await _validationService.ValidatePaymentAmountAsync(
                        request.SaleId, 
                        request.CustomerId, 
                        request.Amount
                    );

                    if (!validationResult.IsValid)
                    {
                        throw new ArgumentException(string.Join(" ", validationResult.Errors));
                    }

                    // Additional check: prevent negative outstanding
                    var outstanding = invoiceSale.GrandTotal - invoiceSale.PaidAmount;
                    if (outstanding <= 0)
                        throw new ArgumentException("Invoice is already fully paid");
                }

                // Determine payment status based on mode
                PaymentStatus paymentStatus;
                if (request.Mode == "CHEQUE")
                    paymentStatus = PaymentStatus.PENDING;
                else if (request.Mode == "CASH" || request.Mode == "ONLINE")
                    paymentStatus = PaymentStatus.CLEARED;
                else if (request.Mode == "CREDIT")
                    paymentStatus = PaymentStatus.PENDING;
                else
                    paymentStatus = PaymentStatus.PENDING;

                // Create payment record using EF Core (PostgreSQL compatible - NO raw SQL)
                var paymentMode = Enum.Parse<PaymentMode>(request.Mode);
                var paymentDate = request.PaymentDate ?? DateTime.UtcNow;
                
                // Create payment using EF Core (works with PostgreSQL)
                var payment = new Payment
                {
                    SaleId = request.SaleId,
                    CustomerId = request.CustomerId.Value,
                    Amount = request.Amount,
                    Mode = paymentMode,
                    Reference = request.Reference,
                    Status = paymentStatus,
                    PaymentDate = paymentDate,
                    CreatedBy = userId,
                    CreatedAt = paymentDate,
                    UpdatedAt = paymentDate
                    // RowVersion will be auto-generated by PostgreSQL trigger
                };
                
                _context.Payments.Add(payment);
                await _context.SaveChangesAsync(); // Save to get payment ID generated
                
                int paymentId = payment.Id;
                Console.WriteLine($"✅ Payment inserted via EF Core. Payment ID: {paymentId}, Amount: {request.Amount}");

                // Update invoice if provided and payment is immediately cleared
                Sale? updatedSale = null;
                if (request.SaleId.HasValue && (paymentStatus == PaymentStatus.CLEARED))
                {
                    // Use the already-fetched invoiceSale to avoid re-fetching
                    if (invoiceSale != null)
                    {
                        // Use optimistic concurrency - check current paid amount
                        var currentPaidAmount = invoiceSale.PaidAmount;
                        invoiceSale.PaidAmount = currentPaidAmount + request.Amount;
                        invoiceSale.LastPaymentDate = payment.PaymentDate;

                        // Update status
                        if (invoiceSale.PaidAmount >= invoiceSale.GrandTotal)
                        {
                            invoiceSale.PaymentStatus = SalePaymentStatus.Paid;
                        }
                        else if (invoiceSale.PaidAmount > 0)
                        {
                            invoiceSale.PaymentStatus = SalePaymentStatus.Partial;
                        }
                        else
                        {
                            invoiceSale.PaymentStatus = SalePaymentStatus.Pending;
                        }

                        updatedSale = invoiceSale;
                    }
                }

                // Update customer balance (only if payment is cleared)
                Customer? updatedCustomer = null;
                if (paymentStatus == PaymentStatus.CLEARED)
                {
                    var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                    if (customer != null)
                    {
                        customer.Balance = Math.Round(customer.Balance - request.Amount, 2, MidpointRounding.AwayFromZero); // Reduce balance (customer owes less)
                        customer.LastActivity = DateTime.UtcNow;
                        customer.UpdatedAt = DateTime.UtcNow;
                        updatedCustomer = customer;
                    }
                }

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Payment Created",
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PaymentId = payment.Id,
                        InvoiceId = request.SaleId,
                        CustomerId = request.CustomerId,
                        Amount = request.Amount,
                        Mode = request.Mode,
                        Status = paymentStatus.ToString(),
                        Reference = request.Reference
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                // Save changes with optimistic concurrency check
                try
                {
                    // Save all changes (payment, invoice, customer, audit)
                    await _context.SaveChangesAsync();
                    
                    // Create idempotency record if key provided
                    if (!string.IsNullOrEmpty(idempotencyKey))
                    {
                        var responseSnapshot = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            PaymentId = paymentId,
                            InvoiceId = request.SaleId,
                            CustomerId = request.CustomerId,
                            Amount = request.Amount
                        });
                        
                        var paymentIdempotency = new PaymentIdempotency
                        {
                            IdempotencyKey = idempotencyKey,
                            PaymentId = paymentId,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            ResponseSnapshot = responseSnapshot
                        };
                        
                        _context.PaymentIdempotencies.Add(paymentIdempotency);
                        await _context.SaveChangesAsync();
                    }
                    
                    await transaction.CommitAsync();
                    Console.WriteLine($"✅ Payment transaction committed. Payment ID: {paymentId}, Amount: {request.Amount}");
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    Console.WriteLine($"❌ Concurrency conflict in payment creation: {ex.Message}");
                    throw new InvalidOperationException("Invoice was modified by another user. Please refresh and try again.", ex);
                }
                catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
                {
                    await transaction.RollbackAsync();
                    var errorMessage = ex.InnerException?.Message ?? ex.Message;
                    Console.WriteLine($"❌ Database update error in payment creation: {errorMessage}");
                    Console.WriteLine($"❌ Full Exception: {ex}");
                    throw new InvalidOperationException($"Database error: {errorMessage}", ex);
                }

                // Reload entities for response
                // Note: payment was inserted via raw SQL, so reload it
                if (updatedSale != null)
                    await _context.Entry(updatedSale).ReloadAsync();
                if (updatedCustomer != null)
                    await _context.Entry(updatedCustomer).ReloadAsync();

                // ALWAYS recalculate customer balance for accuracy (after all transactions)
                // This ensures balance is correct regardless of payment status
                if (request.CustomerId.HasValue)
                {
                    try
                    {
                        var customerService = new Services.CustomerService(_context);
                        await customerService.RecalculateCustomerBalanceAsync(request.CustomerId.Value);
                        // Reload customer after recalculation
                        var recalculatedCustomer = await _context.Customers.FindAsync(request.CustomerId.Value);
                        if (recalculatedCustomer != null)
                            updatedCustomer = recalculatedCustomer;
                        Console.WriteLine($"✅ Customer balance recalculated. New balance: {updatedCustomer?.Balance}");
                    }
                    catch (Exception recalcEx)
                    {
                        // Log but don't fail payment creation
                        Console.WriteLine($"⚠️ Balance recalculation warning: {recalcEx.Message}");
                    }
                }

                return new CreatePaymentResponse
                {
                    Payment = await GetPaymentByIdAsync(paymentId) ?? throw new InvalidOperationException("Failed to retrieve payment"),
                    Invoice = updatedSale != null ? new InvoiceSummaryDto
                    {
                        Id = updatedSale.Id,
                        InvoiceNo = updatedSale.InvoiceNo,
                        TotalAmount = updatedSale.GrandTotal,
                        PaidAmount = updatedSale.PaidAmount,
                        OutstandingAmount = updatedSale.GrandTotal - updatedSale.PaidAmount,
                        Status = updatedSale.PaymentStatus.ToString()
                    } : null,
                    Customer = updatedCustomer != null ? new CustomerSummaryDto
                    {
                        Id = updatedCustomer.Id,
                        Name = updatedCustomer.Name,
                        Balance = updatedCustomer.Balance
                    } : null
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                Console.WriteLine($"❌ PaymentService.CreatePaymentAsync Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                    Console.WriteLine($"❌ Inner Stack Trace: {ex.InnerException.StackTrace}");
                }
                _logger.LogError(ex, "Error creating payment");
                throw;
            }
        }

        public async Task<bool> UpdatePaymentStatusAsync(int paymentId, PaymentStatus newStatus, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Sale)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null) return false;

                var oldStatus = payment.Status;
                payment.Status = newStatus;
                payment.UpdatedAt = DateTime.UtcNow;

                // If changing from PENDING to CLEARED, apply to invoice and balance
                if (oldStatus == PaymentStatus.PENDING && newStatus == PaymentStatus.CLEARED)
                {
                    if (payment.SaleId.HasValue)
                    {
                        var sale = await _context.Sales.FindAsync(payment.SaleId.Value);
                        if (sale != null)
                        {
                            sale.PaidAmount += payment.Amount;
                            sale.LastPaymentDate = payment.PaymentDate;

                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                        }
                    }

                    if (payment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance -= payment.Amount;
                            customer.LastActivity = DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }
                // If voiding, reverse the effects
                else if (newStatus == PaymentStatus.VOID && oldStatus == PaymentStatus.CLEARED)
                {
                    if (payment.SaleId.HasValue)
                    {
                        var sale = await _context.Sales.FindAsync(payment.SaleId.Value);
                        if (sale != null)
                        {
                            sale.PaidAmount = Math.Max(0, sale.PaidAmount - payment.Amount);
                            sale.LastPaymentDate = await _context.Payments
                                .Where(p => p.SaleId == sale.Id && p.Status == PaymentStatus.CLEARED && p.Id != paymentId)
                                .OrderByDescending(p => p.PaymentDate)
                                .Select(p => p.PaymentDate)
                                .FirstOrDefaultAsync();

                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                            else
                                sale.PaymentStatus = SalePaymentStatus.Pending;
                        }
                    }

                    if (payment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance += payment.Amount; // Reverse: customer owes more
                            customer.LastActivity = DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Payment Status Updated",
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PaymentId = paymentId,
                        OldStatus = oldStatus.ToString(),
                        NewStatus = newStatus.ToString()
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PaymentDto?> UpdatePaymentAsync(int paymentId, UpdatePaymentRequest request, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Sale)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null) return null;

                var oldAmount = payment.Amount;
                var oldStatus = payment.Status;
                var wasCleared = oldStatus == PaymentStatus.CLEARED;

                // Reverse old payment effects if it was cleared
                if (wasCleared)
                {
                    if (payment.SaleId.HasValue)
                    {
                        var sale = await _context.Sales.FindAsync(payment.SaleId.Value);
                        if (sale != null)
                        {
                            sale.PaidAmount = Math.Max(0, sale.PaidAmount - oldAmount);
                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                            else
                                sale.PaymentStatus = SalePaymentStatus.Pending;
                        }
                    }

                    if (payment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance += oldAmount; // Reverse: customer owes more
                        }
                    }
                }

                // Update payment fields
                if (request.Amount.HasValue && request.Amount.Value > 0)
                    payment.Amount = request.Amount.Value;

                if (!string.IsNullOrEmpty(request.Mode))
                {
                    if (Enum.TryParse<PaymentMode>(request.Mode, out var mode))
                        payment.Mode = mode;
                }

                if (request.Reference != null)
                    payment.Reference = request.Reference;

                if (request.PaymentDate.HasValue)
                    payment.PaymentDate = request.PaymentDate.Value;

                payment.UpdatedAt = DateTime.UtcNow;

                // Determine new status based on mode (if changed)
                if (!string.IsNullOrEmpty(request.Mode))
                {
                    if (request.Mode == "CHEQUE" || request.Mode == "CREDIT")
                        payment.Status = PaymentStatus.PENDING;
                    else if (request.Mode == "CASH" || request.Mode == "ONLINE")
                        payment.Status = PaymentStatus.CLEARED;
                }

                var newAmount = payment.Amount;
                var newStatus = payment.Status;
                var isNowCleared = newStatus == PaymentStatus.CLEARED;

                // Apply new payment effects if now cleared
                if (isNowCleared)
                {
                    if (payment.SaleId.HasValue)
                    {
                        var sale = await _context.Sales.FindAsync(payment.SaleId.Value);
                        if (sale != null)
                        {
                            sale.PaidAmount = Math.Min(sale.GrandTotal, sale.PaidAmount + newAmount);
                            sale.LastPaymentDate = payment.PaymentDate;

                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                        }
                    }

                    if (payment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance -= newAmount; // Customer owes less
                            customer.LastActivity = DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Payment Updated",
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PaymentId = paymentId,
                        OldAmount = oldAmount,
                        NewAmount = newAmount,
                        OldStatus = oldStatus.ToString(),
                        NewStatus = newStatus.ToString()
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                // Recalculate customer balance for accuracy
                if (payment.CustomerId.HasValue)
                {
                    try
                    {
                        var customerService = new Services.CustomerService(_context);
                        await customerService.RecalculateCustomerBalanceAsync(payment.CustomerId.Value);
                    }
                    catch (Exception recalcEx)
                    {
                        Console.WriteLine($"⚠️ Balance recalculation warning: {recalcEx.Message}");
                    }
                }

                await transaction.CommitAsync();

                return await GetPaymentByIdAsync(paymentId);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeletePaymentAsync(int paymentId, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var payment = await _context.Payments
                    .Include(p => p.Sale)
                    .Include(p => p.Customer)
                    .FirstOrDefaultAsync(p => p.Id == paymentId);

                if (payment == null) return false;

                var wasCleared = payment.Status == PaymentStatus.CLEARED;

                // Reverse payment effects if it was cleared
                if (wasCleared)
                {
                    if (payment.SaleId.HasValue)
                    {
                        var sale = await _context.Sales.FindAsync(payment.SaleId.Value);
                        if (sale != null)
                        {
                            sale.PaidAmount = Math.Max(0, sale.PaidAmount - payment.Amount);
                            sale.LastPaymentDate = await _context.Payments
                                .Where(p => p.SaleId == sale.Id && p.Status == PaymentStatus.CLEARED && p.Id != paymentId)
                                .OrderByDescending(p => p.PaymentDate)
                                .Select(p => p.PaymentDate)
                                .FirstOrDefaultAsync();

                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                            else
                                sale.PaymentStatus = SalePaymentStatus.Pending;
                        }
                    }

                    if (payment.CustomerId.HasValue)
                    {
                        var customer = await _context.Customers.FindAsync(payment.CustomerId.Value);
                        if (customer != null)
                        {
                            customer.Balance += payment.Amount; // Reverse: customer owes more
                            customer.LastActivity = DateTime.UtcNow;
                            customer.UpdatedAt = DateTime.UtcNow;
                        }
                    }
                }

                // Delete idempotency records
                var idempotencies = await _context.PaymentIdempotencies
                    .Where(pi => pi.PaymentId == paymentId)
                    .ToListAsync();
                _context.PaymentIdempotencies.RemoveRange(idempotencies);

                // Delete payment
                _context.Payments.Remove(payment);

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Payment Deleted",
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        PaymentId = paymentId,
                        Amount = payment.Amount,
                        Mode = payment.Mode.ToString(),
                        Status = payment.Status.ToString(),
                        SaleId = payment.SaleId,
                        CustomerId = payment.CustomerId
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);
                await _context.SaveChangesAsync();

                // Recalculate customer balance for accuracy
                if (payment.CustomerId.HasValue)
                {
                    try
                    {
                        var customerService = new Services.CustomerService(_context);
                        await customerService.RecalculateCustomerBalanceAsync(payment.CustomerId.Value);
                    }
                    catch (Exception recalcEx)
                    {
                        Console.WriteLine($"⚠️ Balance recalculation warning: {recalcEx.Message}");
                    }
                }

                await transaction.CommitAsync();
                return true;
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<List<Models.OutstandingInvoiceDto>> GetOutstandingInvoicesAsync(int customerId)
        {
            var invoices = await _context.Sales
                .Where(s => s.CustomerId == customerId && 
                           !s.IsDeleted &&
                           (s.PaymentStatus == SalePaymentStatus.Pending || s.PaymentStatus == SalePaymentStatus.Partial))
                .Select(s => new Models.OutstandingInvoiceDto
                {
                    Id = s.Id,
                    InvoiceNo = s.InvoiceNo,
                    InvoiceDate = s.InvoiceDate,
                    GrandTotal = s.GrandTotal,
                    PaidAmount = s.PaidAmount,
                    BalanceAmount = s.GrandTotal - s.PaidAmount,
                    PaymentStatus = s.PaymentStatus.ToString(),
                    DaysOverdue = (int)(DateTime.UtcNow - s.InvoiceDate).TotalDays
                })
                .OrderBy(s => s.InvoiceDate)
                .ToListAsync();

            return invoices;
        }

        public async Task<InvoiceAmountDto> GetInvoiceAmountAsync(int invoiceId)
        {
            var sale = await _context.Sales.FindAsync(invoiceId);
            if (sale == null)
                throw new ArgumentException("Invoice not found");

            return new InvoiceAmountDto
            {
                Id = sale.Id,
                InvoiceNo = sale.InvoiceNo,
                TotalAmount = sale.GrandTotal,
                PaidAmount = sale.PaidAmount,
                OutstandingAmount = sale.GrandTotal - sale.PaidAmount,
                Status = sale.PaymentStatus.ToString()
            };
        }

        public async Task<CreatePaymentResponse> AllocatePaymentAsync(AllocatePaymentRequest request, int userId, string? idempotencyKey = null)
        {
            if (request.Amount <= 0)
                throw new ArgumentException("Payment amount must be greater than zero");

            if (!request.CustomerId.HasValue)
                throw new ArgumentException("Customer ID is required");

            // Check idempotency if key provided
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                var existingRequest = await _context.PaymentIdempotencies
                    .FirstOrDefaultAsync(pr => pr.IdempotencyKey == idempotencyKey);
                
                if (existingRequest != null)
                {
                    var existingPayment = await GetPaymentByIdAsync(existingRequest.PaymentId);
                    if (existingPayment != null)
                    {
                        var sale = existingRequest.Payment?.Sale;
                        var customer = existingRequest.Payment?.Customer;
                        
                        return new CreatePaymentResponse
                        {
                            Payment = existingPayment,
                            Invoice = sale != null ? new InvoiceSummaryDto
                            {
                                Id = sale.Id,
                                InvoiceNo = sale.InvoiceNo,
                                TotalAmount = sale.GrandTotal,
                                PaidAmount = sale.PaidAmount,
                                OutstandingAmount = sale.GrandTotal - sale.PaidAmount,
                                Status = sale.PaymentStatus.ToString()
                            } : null,
                            Customer = customer != null ? new CustomerSummaryDto
                            {
                                Id = customer.Id,
                                Name = customer.Name,
                                Balance = customer.Balance
                            } : null
                        };
                    }
                }
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var customer = await _context.Customers.FindAsync(request.CustomerId.Value);
                if (customer == null)
                    throw new ArgumentException("Customer not found");

                // Get outstanding invoices ordered by date (oldest first)
                var outstandingInvoices = await GetOutstandingInvoicesAsync(request.CustomerId.Value);
                outstandingInvoices = outstandingInvoices.OrderBy(i => i.InvoiceDate).ToList();

                decimal remainingAmount = request.Amount;
                var allocatedPayments = new List<Payment>();

                // Allocate to invoices
                foreach (var allocation in request.Allocations ?? new List<AllocationItem>())
                {
                    if (remainingAmount <= 0) break;

                    var invoice = outstandingInvoices.FirstOrDefault(i => i.Id == allocation.InvoiceId);
                    if (invoice == null) continue;

                    var allocationAmount = Math.Min(allocation.Amount, Math.Min(remainingAmount, invoice.BalanceAmount));

                    if (allocationAmount <= 0) continue;

                    // Determine payment status
                    PaymentStatus paymentStatus;
                    if (request.Mode == "CHEQUE")
                        paymentStatus = PaymentStatus.PENDING;
                    else if (request.Mode == "CASH" || request.Mode == "ONLINE")
                        paymentStatus = PaymentStatus.CLEARED;
                    else
                        paymentStatus = PaymentStatus.PENDING;

                    // Create payment - use raw SQL to insert both old and new columns during transition
                    var modeValue = request.Mode;
                    var statusValue = paymentStatus.ToString();
                    var methodValue = modeValue; // Sync old Method column
                    var chequeStatusValue = paymentStatus switch
                    {
                        PaymentStatus.PENDING => "Pending",
                        PaymentStatus.CLEARED => "Cleared",
                        PaymentStatus.RETURNED => "Returned",
                        PaymentStatus.VOID => "Cleared",
                        _ => "Pending"
                    };

                    var paymentDate = request.PaymentDate ?? DateTime.UtcNow;
                    
                    // Create payment using EF Core (will handle Mode/Status columns)
                    var payment = new Payment
                    {
                        SaleId = allocation.InvoiceId,
                        CustomerId = request.CustomerId.Value,
                        Amount = Math.Round(allocationAmount, 2, MidpointRounding.AwayFromZero),
                        Mode = Enum.Parse<PaymentMode>(modeValue),
                        Reference = request.Reference,
                        Status = paymentStatus,
                        PaymentDate = paymentDate,
                        CreatedBy = userId,
                        CreatedAt = DateTime.UtcNow
                    };

                    _context.Payments.Add(payment);
                    await _context.SaveChangesAsync(); // Save to get ID
                    
                    allocatedPayments.Add(payment);

                    // Update invoice if payment is cleared
                    if (paymentStatus == PaymentStatus.CLEARED)
                    {
                        var sale = await _context.Sales.FindAsync(allocation.InvoiceId);
                        if (sale != null)
                        {
                            sale.PaidAmount = Math.Round(sale.PaidAmount + allocationAmount, 2, MidpointRounding.AwayFromZero);
                            sale.LastPaymentDate = payment.PaymentDate;

                            if (sale.PaidAmount >= sale.GrandTotal)
                                sale.PaymentStatus = SalePaymentStatus.Paid;
                            else if (sale.PaidAmount > 0)
                                sale.PaymentStatus = SalePaymentStatus.Partial;
                        }
                    }

                    remainingAmount -= allocationAmount;
                }

                // ALWAYS recalculate customer balance for accuracy (after all transactions)
                // This ensures balance is correct regardless of payment status
                if (request.CustomerId.HasValue)
                {
                    try
                    {
                        var customerService = new Services.CustomerService(_context);
                        await customerService.RecalculateCustomerBalanceAsync(request.CustomerId.Value);
                        // Reload customer after recalculation
                        await _context.Entry(customer).ReloadAsync();
                        Console.WriteLine($"✅ Customer balance recalculated after allocation. New balance: {customer.Balance}");
                    }
                    catch (Exception recalcEx)
                    {
                        // Log but don't fail payment allocation
                        Console.WriteLine($"⚠️ Balance recalculation warning: {recalcEx.Message}");
                    }
                }

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Bulk Payment Allocated",
                    Details = System.Text.Json.JsonSerializer.Serialize(new
                    {
                        CustomerId = request.CustomerId,
                        TotalAmount = request.Amount,
                        Mode = request.Mode,
                        Allocations = request.Allocations
                    }),
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                // Save changes with optimistic concurrency check
                try
                {
                    await _context.SaveChangesAsync();
                    
                    // Create idempotency record if key provided
                    if (!string.IsNullOrEmpty(idempotencyKey) && allocatedPayments.Any())
                    {
                        var firstPayment = allocatedPayments.First();
                        var responseSnapshot = System.Text.Json.JsonSerializer.Serialize(new
                        {
                            PaymentId = firstPayment.Id,
                            CustomerId = request.CustomerId,
                            TotalAmount = request.Amount,
                            Allocations = request.Allocations
                        });
                        
                        var paymentIdempotency = new PaymentIdempotency
                        {
                            IdempotencyKey = idempotencyKey,
                            PaymentId = firstPayment.Id,
                            UserId = userId,
                            CreatedAt = DateTime.UtcNow,
                            ResponseSnapshot = responseSnapshot
                        };
                        
                        _context.PaymentIdempotencies.Add(paymentIdempotency);
                        await _context.SaveChangesAsync();
                    }
                    
                    await transaction.CommitAsync();
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    await transaction.RollbackAsync();
                    throw new InvalidOperationException("Invoice was modified by another user. Please refresh and try again.", ex);
                }

                // Reload customer
                await _context.Entry(customer).ReloadAsync();

                // CRITICAL FIX: Validate allocatedPayments is not empty before accessing
                if (allocatedPayments.Count == 0)
                {
                    throw new InvalidOperationException("No payments were allocated. Please check invoice allocation criteria.");
                }

                return new CreatePaymentResponse
                {
                    Payment = await GetPaymentByIdAsync(allocatedPayments.First().Id) ?? throw new InvalidOperationException("Failed to retrieve payment"),
                    Customer = new CustomerSummaryDto
                    {
                        Id = customer.Id,
                        Name = customer.Name,
                        Balance = customer.Balance
                    }
                };
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error allocating payment");
                throw;
            }
        }
    }

    // DTOs
    public class PaymentDto
    {
        public int Id { get; set; }
        public int? SaleId { get; set; }
        public string? InvoiceNo { get; set; }
        public int? CustomerId { get; set; }
        public string? CustomerName { get; set; }
        public decimal Amount { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime PaymentDate { get; set; }
        public int CreatedBy { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreatePaymentRequest
    {
        public int? SaleId { get; set; }
        public int? CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Mode { get; set; } = string.Empty; // CASH, CHEQUE, ONLINE, CREDIT
        public string? Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public class UpdatePaymentRequest
    {
        public decimal? Amount { get; set; }
        public string? Mode { get; set; } // CASH, CHEQUE, ONLINE, CREDIT
        public string? Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
    }

    public class CreatePaymentResponse
    {
        public PaymentDto Payment { get; set; } = null!;
        public InvoiceSummaryDto? Invoice { get; set; }
        public CustomerSummaryDto? Customer { get; set; }
    }

    public class InvoiceSummaryDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class CustomerSummaryDto
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Balance { get; set; }
    }

    // OutstandingInvoiceDto moved to FrozenApi.Models.DTOs to avoid duplication

    public class InvoiceAmountDto
    {
        public int Id { get; set; }
        public string InvoiceNo { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public string Status { get; set; } = string.Empty;
    }

    public class AllocatePaymentRequest
    {
        public int? CustomerId { get; set; }
        public decimal Amount { get; set; }
        public string Mode { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public DateTime? PaymentDate { get; set; }
        public List<AllocationItem>? Allocations { get; set; }
    }

    public class AllocationItem
    {
        public int InvoiceId { get; set; }
        public decimal Amount { get; set; }
    }
}
