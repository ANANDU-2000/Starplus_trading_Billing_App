/*
Purpose: Purchase service for supplier purchase management
Author: AI Assistant
Date: 2024
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IPurchaseService
    {
        Task<PagedResponse<PurchaseDto>> GetPurchasesAsync(int page = 1, int pageSize = 10);
        Task<PurchaseDto?> GetPurchaseByIdAsync(int id);
        Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, int userId);
        Task<PurchaseDto?> UpdatePurchaseAsync(int id, CreatePurchaseRequest request, int userId);
        Task<bool> DeletePurchaseAsync(int id, int userId);
    }

    public class PurchaseService : IPurchaseService
    {
        private readonly AppDbContext _context;

        public PurchaseService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<PagedResponse<PurchaseDto>> GetPurchasesAsync(int page = 1, int pageSize = 10)
        {
            var query = _context.Purchases
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                .AsQueryable();

            var totalCount = await query.CountAsync();
            var purchases = await query
                .OrderByDescending(p => p.PurchaseDate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PurchaseDto
                {
                    Id = p.Id,
                    SupplierName = p.SupplierName,
                    InvoiceNo = p.InvoiceNo,
                    PurchaseDate = p.PurchaseDate,
                    ExpenseCategory = p.ExpenseCategory,
                    Subtotal = p.Subtotal,
                    VatTotal = p.VatTotal,
                    TotalAmount = p.TotalAmount,
                    Items = p.Items.Select(i => new PurchaseItemDto
                    {
                        Id = i.Id,
                        ProductId = i.ProductId,
                        ProductName = i.Product.NameEn,
                        UnitType = i.UnitType,
                        Qty = i.Qty,
                        UnitCost = i.UnitCost,
                        UnitCostExclVat = i.UnitCostExclVat,
                        VatAmount = i.VatAmount,
                        LineTotal = i.LineTotal
                    }).ToList()
                })
                .ToListAsync();

            return new PagedResponse<PurchaseDto>
            {
                Items = purchases,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<PurchaseDto?> GetPurchaseByIdAsync(int id)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                    .ThenInclude(i => i.Product)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null) return null;

            return new PurchaseDto
            {
                Id = purchase.Id,
                SupplierName = purchase.SupplierName,
                InvoiceNo = purchase.InvoiceNo,
                PurchaseDate = purchase.PurchaseDate,
                ExpenseCategory = purchase.ExpenseCategory,
                Subtotal = purchase.Subtotal,
                VatTotal = purchase.VatTotal,
                TotalAmount = purchase.TotalAmount,
                Items = purchase.Items.Select(i => new PurchaseItemDto
                {
                    Id = i.Id,
                    ProductId = i.ProductId,
                    ProductName = i.Product.NameEn,
                    UnitType = i.UnitType,
                    Qty = i.Qty,
                    UnitCost = i.UnitCost,
                    UnitCostExclVat = i.UnitCostExclVat,
                    VatAmount = i.VatAmount,
                    LineTotal = i.LineTotal
                }).ToList()
            };
        }

        public async Task<PurchaseDto> CreatePurchaseAsync(CreatePurchaseRequest request, int userId)
        {
            // DEFENSIVE VALIDATION: Check request integrity
            if (request == null)
                throw new ArgumentNullException(nameof(request), "Request cannot be null");
            
            if (string.IsNullOrWhiteSpace(request.SupplierName))
                throw new InvalidOperationException("Supplier name is required");
            
            if (string.IsNullOrWhiteSpace(request.InvoiceNo))
                throw new InvalidOperationException("Invoice number is required");
            
            if (request.Items == null || request.Items.Count == 0)
                throw new InvalidOperationException("Purchase must have at least one item");
            
            // Validate each item has required fields
            foreach (var item in request.Items)
            {
                if (item.ProductId <= 0)
                    throw new InvalidOperationException($"Invalid product ID: {item.ProductId}");
                
                if (item.Qty <= 0)
                    throw new InvalidOperationException($"Quantity must be positive");
                
                if (item.UnitCost < 0)
                    throw new InvalidOperationException($"Unit cost cannot be negative");
                
                if (string.IsNullOrWhiteSpace(item.UnitType))
                    throw new InvalidOperationException("Unit type is required for all items");
            }
            
            // Validate unique supplier + invoice number
            var existing = await _context.Purchases
                .FirstOrDefaultAsync(p => 
                    p.SupplierName.ToLower() == request.SupplierName.ToLower() && 
                    p.InvoiceNo == request.InvoiceNo);
            
            if (existing != null)
            {
                throw new InvalidOperationException(
                    $"Purchase invoice '{request.InvoiceNo}' from supplier '{request.SupplierName}' already exists.");
            }
            
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // VAT CALCULATION LOGIC
                // CRITICAL: Purchase bills show Unit Cost EXCLUDING VAT (like sales invoices)
                // Default: Costs EXCLUDE VAT (matching real purchase invoices)
                bool includesVat = request.IncludesVat ?? false; // Changed from true to false
                decimal vatPercent = request.VatPercent ?? 5m; // Default 5% VAT for UAE
                
                decimal subtotal = 0;
                decimal vatTotal = 0;
                decimal totalAmount = 0;
                var purchaseItems = new List<PurchaseItem>();
                var inventoryTransactions = new List<InventoryTransaction>();

                foreach (var item in request.Items)
                {
                    // CRITICAL: Verify product exists before updating stock
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found. Please verify the product exists.");

                    // Calculate quantities safely
                    if (item.Qty <= 0 || item.Qty > 1000000)
                        throw new InvalidOperationException($"Invalid quantity {item.Qty} for product '{product.NameEn}'. Must be between 0.01 and 1,000,000.");
                    
                    var baseQty = item.Qty * product.ConversionToBase;
                    if (baseQty <= 0)
                        throw new InvalidOperationException($"Calculated base quantity is invalid. Please check conversion ratio for product '{product.NameEn}'.");
                    
                    // Validate cost
                    if (item.UnitCost < 0 || item.UnitCost > 10000000)
                        throw new InvalidOperationException($"Invalid unit cost {item.UnitCost} for product '{product.NameEn}'. Must be between 0 and 10,000,000.");

                    // CRITICAL VAT CALCULATION
                    decimal unitCostExclVat;
                    decimal unitCostInclVat;
                    decimal itemVatAmount;
                    
                    if (includesVat)
                    {
                        // Cost includes VAT - need to extract VAT amount
                        // Formula: UnitCostExclVat = UnitCost / (1 + VatPercent/100)
                        unitCostInclVat = item.UnitCost;
                        unitCostExclVat = item.UnitCost / (1 + (vatPercent / 100));
                        itemVatAmount = unitCostInclVat - unitCostExclVat;
                    }
                    else
                    {
                        // Cost excludes VAT - need to add VAT
                        unitCostExclVat = item.UnitCost;
                        itemVatAmount = unitCostExclVat * (vatPercent / 100);
                        unitCostInclVat = unitCostExclVat + itemVatAmount;
                    }
                    
                    var lineSubtotal = item.Qty * unitCostExclVat;
                    var lineVat = item.Qty * itemVatAmount;
                    var lineTotal = item.Qty * unitCostInclVat;
                    
                    subtotal += lineSubtotal;
                    vatTotal += lineVat;
                    totalAmount += lineTotal;

                    var purchaseItem = new PurchaseItem
                    {
                        ProductId = item.ProductId,
                        UnitType = item.UnitType,
                        Qty = item.Qty,
                        UnitCost = unitCostInclVat, // Store cost INCLUDING VAT for backward compatibility
                        UnitCostExclVat = unitCostExclVat, // NEW: Cost excluding VAT
                        VatAmount = itemVatAmount, // NEW: VAT amount per unit
                        LineTotal = lineTotal
                    };

                    purchaseItems.Add(purchaseItem);

                    // Calculate base quantity and update stock (reuse validated baseQty from above)
                    product.StockQty += baseQty;
                    product.UpdatedAt = DateTime.UtcNow;
                    
                    // CRITICAL: Update cost price with VAT-EXCLUDED cost
                    // This ensures profit calculations are accurate
                    if (unitCostExclVat > 0)
                    {
                        // Update cost price based on base unit (EXCLUDING VAT)
                        var costPerBaseUnit = unitCostExclVat / product.ConversionToBase;
                        product.CostPrice = costPerBaseUnit;
                        
                        Console.WriteLine($"📊 Product {product.NameEn}: CostPrice updated to {costPerBaseUnit:C} (excl. VAT) - was using {item.UnitCost:C} (incl. VAT)");
                    }

                    // Create inventory transaction
                    var inventoryTransaction = new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        ChangeQty = baseQty,
                        TransactionType = TransactionType.Purchase,
                        RefId = null, // Will be updated after purchase is created
                        CreatedAt = DateTime.UtcNow
                    };

                    inventoryTransactions.Add(inventoryTransaction);
                }

                // Log purchase creation attempt
                Console.WriteLine($"✅ Purchase validation passed: Supplier={request.SupplierName}, Invoice={request.InvoiceNo}, Items={request.Items.Count}");
                Console.WriteLine($"💰 Subtotal={subtotal:C}, VAT={vatTotal:C} ({vatPercent}%), Total={totalAmount:C}");
                
                var purchase = new Purchase
                {
                    SupplierName = request.SupplierName,
                    InvoiceNo = request.InvoiceNo,
                    PurchaseDate = request.PurchaseDate,
                    ExpenseCategory = request.ExpenseCategory, // Track expense category
                    Subtotal = subtotal, // NEW: Amount before VAT
                    VatTotal = vatTotal, // NEW: VAT amount
                    TotalAmount = totalAmount, // Grand total (for backward compatibility)
                    CreatedBy = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.Purchases.Add(purchase);
                await _context.SaveChangesAsync();

                // Update purchase items with purchase ID
                foreach (var item in purchaseItems)
                {
                    item.PurchaseId = purchase.Id;
                }

                _context.PurchaseItems.AddRange(purchaseItems);

                // Update inventory transactions with purchase ID
                foreach (var invTx in inventoryTransactions)
                {
                    invTx.RefId = purchase.Id;
                }

                _context.InventoryTransactions.AddRange(inventoryTransactions);

                // Create audit log
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Purchase Created",
                    Details = $"Supplier: {request.SupplierName}, Invoice: {request.InvoiceNo}, Total: {totalAmount:C}",
                    CreatedAt = DateTime.UtcNow
                };

                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetPurchaseByIdAsync(purchase.Id) ?? throw new InvalidOperationException("Failed to retrieve created purchase");
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<PurchaseDto?> UpdatePurchaseAsync(int id, CreatePurchaseRequest request, int userId)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
                return null;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Reverse old stock changes
                foreach (var oldItem in purchase.Items)
                {
                    var product = await _context.Products.FindAsync(oldItem.ProductId);
                    if (product != null)
                    {
                        var oldBaseQty = oldItem.Qty * product.ConversionToBase;
                        product.StockQty -= oldBaseQty; // Reverse old purchase
                    }
                }

                // Remove old items and transactions
                _context.PurchaseItems.RemoveRange(purchase.Items);
                var oldTransactions = await _context.InventoryTransactions
                    .Where(t => t.RefId == id && t.TransactionType == TransactionType.Purchase)
                    .ToListAsync();
                _context.InventoryTransactions.RemoveRange(oldTransactions);

                // Update purchase details
                purchase.SupplierName = request.SupplierName;
                purchase.InvoiceNo = request.InvoiceNo ?? purchase.InvoiceNo;
                purchase.PurchaseDate = request.PurchaseDate;
                purchase.ExpenseCategory = request.ExpenseCategory;

                // VAT CALCULATION LOGIC (same as CreatePurchase)
                // CRITICAL: Purchase bills show Unit Cost EXCLUDING VAT (like sales invoices)
                bool includesVat = request.IncludesVat ?? false; // Changed from true to false
                decimal vatPercent = request.VatPercent ?? 5m;
                
                decimal subtotal = 0;
                decimal vatTotal = 0;
                decimal totalAmount = 0;
                
                // Add new items and update stock
                foreach (var item in request.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product == null)
                        throw new InvalidOperationException($"Product with ID {item.ProductId} not found");

                    var baseQty = item.Qty * product.ConversionToBase;
                    
                    // CRITICAL VAT CALCULATION
                    decimal unitCostExclVat;
                    decimal unitCostInclVat;
                    decimal itemVatAmount;
                    
                    if (includesVat)
                    {
                        unitCostInclVat = item.UnitCost;
                        unitCostExclVat = item.UnitCost / (1 + (vatPercent / 100));
                        itemVatAmount = unitCostInclVat - unitCostExclVat;
                    }
                    else
                    {
                        unitCostExclVat = item.UnitCost;
                        itemVatAmount = unitCostExclVat * (vatPercent / 100);
                        unitCostInclVat = unitCostExclVat + itemVatAmount;
                    }
                    
                    var lineSubtotal = item.Qty * unitCostExclVat;
                    var lineVat = item.Qty * itemVatAmount;
                    var lineTotal = item.Qty * unitCostInclVat;
                    
                    subtotal += lineSubtotal;
                    vatTotal += lineVat;
                    totalAmount += lineTotal;

                    var purchaseItem = new PurchaseItem
                    {
                        PurchaseId = id,
                        ProductId = item.ProductId,
                        UnitType = item.UnitType,
                        Qty = item.Qty,
                        UnitCost = unitCostInclVat,
                        UnitCostExclVat = unitCostExclVat,
                        VatAmount = itemVatAmount,
                        LineTotal = lineTotal
                    };
                    _context.PurchaseItems.Add(purchaseItem);

                    // Update stock with new quantity
                    product.StockQty += baseQty;
                    product.UpdatedAt = DateTime.UtcNow;
                    
                    // CRITICAL: Update cost price with VAT-EXCLUDED cost
                    if (unitCostExclVat > 0)
                    {
                        var costPerBaseUnit = unitCostExclVat / product.ConversionToBase;
                        product.CostPrice = costPerBaseUnit;
                    }

                    // Create inventory transaction
                    var inventoryTransaction = new InventoryTransaction
                    {
                        ProductId = item.ProductId,
                        ChangeQty = baseQty,
                        TransactionType = TransactionType.Purchase,
                        RefId = id,
                        Reason = $"Purchase Updated: {request.InvoiceNo}",
                        CreatedAt = DateTime.UtcNow
                    };
                    _context.InventoryTransactions.Add(inventoryTransaction);
                }

                purchase.Subtotal = subtotal;
                purchase.VatTotal = vatTotal;
                purchase.TotalAmount = totalAmount;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return await GetPurchaseByIdAsync(id);
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        public async Task<bool> DeletePurchaseAsync(int id, int userId)
        {
            var purchase = await _context.Purchases
                .Include(p => p.Items)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (purchase == null)
                return false;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // CRITICAL: Reverse all stock changes before deleting
                foreach (var item in purchase.Items)
                {
                    var product = await _context.Products.FindAsync(item.ProductId);
                    if (product != null)
                    {
                        // Calculate base quantity that was added during purchase
                        var baseQty = item.Qty * product.ConversionToBase;
                        
                        // Subtract the purchased quantity to reverse the stock increase
                        product.StockQty -= baseQty;
                        product.UpdatedAt = DateTime.UtcNow;

                        // Log the reversal
                        Console.WriteLine($"🔄 Reversing stock for {product.NameEn}: -{baseQty} (Purchase ID: {id})");
                    }
                }

                // Remove all inventory transactions related to this purchase
                var inventoryTransactions = await _context.InventoryTransactions
                    .Where(t => t.RefId == id && t.TransactionType == TransactionType.Purchase)
                    .ToListAsync();
                _context.InventoryTransactions.RemoveRange(inventoryTransactions);

                // Remove all purchase items
                _context.PurchaseItems.RemoveRange(purchase.Items);

                // Remove the purchase record
                _context.Purchases.Remove(purchase);

                // Create audit log for deletion
                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Purchase Deleted",
                    Details = $"Deleted Purchase: Supplier={purchase.SupplierName}, Invoice={purchase.InvoiceNo}, Total={purchase.TotalAmount:C}, Items={purchase.Items.Count}",
                    CreatedAt = DateTime.UtcNow
                };
                _context.AuditLogs.Add(auditLog);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                Console.WriteLine($"✅ Purchase deleted successfully: ID={id}, Invoice={purchase.InvoiceNo}");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ Purchase deletion failed: {ex.Message}");
                await transaction.RollbackAsync();
                throw;
            }
        }
    }

    public class PurchaseDto
    {
        public int Id { get; set; }
        public string SupplierName { get; set; } = string.Empty;
        public string InvoiceNo { get; set; } = string.Empty;
        public DateTime PurchaseDate { get; set; }
        public string? ExpenseCategory { get; set; } // Expense category
            
        // VAT FIELDS (for accurate reporting)
        public decimal? Subtotal { get; set; } // Amount before VAT
        public decimal? VatTotal { get; set; } // VAT amount
        public decimal TotalAmount { get; set; } // Grand total
            
        public List<PurchaseItemDto> Items { get; set; } = new();
    }

    public class PurchaseItemDto
    {
        public int Id { get; set; }
        public int ProductId { get; set; }
        public string ProductName { get; set; } = string.Empty;
        public string UnitType { get; set; } = string.Empty;
        public decimal Qty { get; set; }
        public decimal UnitCost { get; set; } // Cost INCLUDING VAT (for backward compatibility)
        public decimal? UnitCostExclVat { get; set; } // Cost EXCLUDING VAT
        public decimal? VatAmount { get; set; } // VAT amount for this line
        public decimal LineTotal { get; set; } // Total INCLUDING VAT
    }
}

