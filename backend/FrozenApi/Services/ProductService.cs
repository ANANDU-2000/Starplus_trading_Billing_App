/*
Purpose: Product service for inventory management — normalized search, soft delete, barcode.
*/
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;
using FrozenApi.Helpers;

namespace FrozenApi.Services
{
    public interface IProductService
    {
        Task<PagedResponse<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10, string? search = null, bool lowStock = false, string? unitType = null, bool includeInactive = false);
        Task<ProductDto?> GetProductByIdAsync(int id);
        Task<ProductDto> CreateProductAsync(CreateProductRequest request);
        Task<ProductDto?> UpdateProductAsync(int id, CreateProductRequest request, int? userId = null);
        Task<bool> DeleteProductAsync(int id);
        Task<ProductDto?> ReactivateProductAsync(int id);
        Task<bool> AdjustStockAsync(int productId, decimal changeQty, string reason, int userId);
        Task<List<ProductDto>> GetLowStockProductsAsync();
        Task<List<ProductDto>> SearchProductsAsync(string? query, int limit = 50, bool includeInactive = false);
        Task<List<DuplicateProductGroupDto>> GetDuplicateProductNameGroupsAsync(int maxGroups = 50);
        Task<List<PriceChangeLogDto>> GetPriceChangeHistoryAsync(int productId);
        Task<int> ResetAllStockAsync(int userId);
    }

    public class ProductService : IProductService
    {
        private readonly AppDbContext _context;
        private readonly IInventoryLedgerService _inventoryLedger;

        public ProductService(AppDbContext context, IInventoryLedgerService inventoryLedger)
        {
            _context = context;
            _inventoryLedger = inventoryLedger;
        }

        private static ProductDto MapToDto(Product p) => new()
        {
            Id = p.Id,
            Sku = p.Sku,
            Barcode = p.Barcode,
            IsActive = p.IsActive,
            NameEn = p.NameEn,
            NameAr = p.NameAr,
            UnitType = p.UnitType,
            ConversionToBase = p.ConversionToBase,
            CostPrice = p.CostPrice,
            SellPrice = p.SellPrice,
            StockQty = p.StockQty,
            ReorderLevel = p.ReorderLevel,
            ExpiryDate = p.ExpiryDate,
            DescriptionEn = p.DescriptionEn,
            DescriptionAr = p.DescriptionAr
        };

        private IQueryable<Product> BaseQuery(bool includeInactive) =>
            includeInactive ? _context.Products : _context.Products.Where(p => p.IsActive);

        private static IQueryable<Product> ApplyIlikeSearch(IQueryable<Product> query, string normalizedSearch)
        {
            if (string.IsNullOrEmpty(normalizedSearch)) return query;
            var pattern = ProductSearchHelper.ToContainsPattern(normalizedSearch);
            return query.Where(p =>
                EF.Functions.ILike(p.NameEn, pattern) ||
                (p.NameAr != null && EF.Functions.ILike(p.NameAr, pattern)) ||
                EF.Functions.ILike(p.Sku, pattern) ||
                (p.Barcode != null && EF.Functions.ILike(p.Barcode, pattern)));
        }

        public async Task<PagedResponse<ProductDto>> GetProductsAsync(int page = 1, int pageSize = 10, string? search = null, bool lowStock = false, string? unitType = null, bool includeInactive = false)
        {
            var query = BaseQuery(includeInactive);

            var sq = ProductSearchHelper.NormalizeQuery(search);
            if (!string.IsNullOrEmpty(sq))
                query = ApplyIlikeSearch(query, sq);

            if (lowStock)
                query = query.Where(p => p.StockQty <= p.ReorderLevel);

            if (!string.IsNullOrEmpty(unitType))
                query = query.Where(p => p.UnitType == unitType);

            var totalCount = await query.CountAsync();
            var products = await query
                .OrderBy(p => p.NameEn)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Barcode = p.Barcode,
                    IsActive = p.IsActive,
                    NameEn = p.NameEn,
                    NameAr = p.NameAr,
                    UnitType = p.UnitType,
                    ConversionToBase = p.ConversionToBase,
                    CostPrice = p.CostPrice,
                    SellPrice = p.SellPrice,
                    StockQty = p.StockQty,
                    ReorderLevel = p.ReorderLevel,
                    ExpiryDate = p.ExpiryDate,
                    DescriptionEn = p.DescriptionEn,
                    DescriptionAr = p.DescriptionAr
                })
                .ToListAsync();

            return new PagedResponse<ProductDto>
            {
                Items = products,
                TotalCount = totalCount,
                Page = page,
                PageSize = pageSize,
                TotalPages = (int)Math.Ceiling((double)totalCount / pageSize)
            };
        }

        public async Task<ProductDto?> GetProductByIdAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            return product == null ? null : MapToDto(product);
        }

        private static string NormalizeSku(string sku) => sku.Trim();
        private static string? NormalizeBarcode(string? barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            return barcode.Trim();
        }

        public async Task<ProductDto> CreateProductAsync(CreateProductRequest request)
        {
            if (string.IsNullOrWhiteSpace(request.NameEn))
                throw new InvalidOperationException("Product name is required");

            var sku = NormalizeSku(request.Sku);
            if (!InputValidator.ValidateSKU(sku))
                throw new InvalidOperationException("Invalid SKU format");

            if (!InputValidator.ValidatePrice(request.SellPrice) || !InputValidator.ValidatePrice(request.CostPrice))
                throw new InvalidOperationException("Invalid price. Prices must be between 0 and 1,000,000");

            var skuNorm = sku.ToLowerInvariant();
            if (await _context.Products.AnyAsync(p => p.Sku.ToLower() == skuNorm))
                throw new InvalidOperationException("SKU already exists");

            var barcode = NormalizeBarcode(request.Barcode);
            if (barcode != null)
            {
                barcode = InputValidator.SanitizeString(barcode, 64);
                var bcNorm = barcode.ToLowerInvariant();
                if (await _context.Products.AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == bcNorm))
                    throw new InvalidOperationException("Barcode already exists");
            }

            var product = new Product
            {
                Sku = InputValidator.SanitizeString(sku, 100),
                Barcode = barcode == null ? null : InputValidator.SanitizeString(barcode, 64),
                IsActive = true,
                NameEn = InputValidator.SanitizeString(request.NameEn.Trim(), 200),
                NameAr = InputValidator.SanitizeString(request.NameAr, 200),
                UnitType = InputValidator.SanitizeString(request.UnitType, 50),
                ConversionToBase = request.ConversionToBase > 0 ? request.ConversionToBase : 1,
                CostPrice = request.CostPrice >= 0 ? request.CostPrice : 0,
                SellPrice = request.SellPrice >= 0 ? request.SellPrice : 0,
                StockQty = request.StockQty >= 0 ? request.StockQty : 0,
                ReorderLevel = request.ReorderLevel >= 0 ? request.ReorderLevel : 0,
                DescriptionEn = InputValidator.SanitizeString(request.DescriptionEn, 1000),
                DescriptionAr = InputValidator.SanitizeString(request.DescriptionAr, 1000),
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            _context.Products.Add(product);
            await _context.SaveChangesAsync();

            return MapToDto(product);
        }

        public async Task<ProductDto?> UpdateProductAsync(int id, CreateProductRequest request, int? userId = null)
        {
            if (string.IsNullOrWhiteSpace(request.NameEn))
                throw new InvalidOperationException("Product name is required");

            var sku = NormalizeSku(request.Sku);
            if (!InputValidator.ValidateSKU(sku))
                throw new InvalidOperationException("Invalid SKU format");

            if (!InputValidator.ValidatePrice(request.SellPrice) || !InputValidator.ValidatePrice(request.CostPrice))
                throw new InvalidOperationException("Invalid price. Prices must be between 0 and 1,000,000");

            var product = await _context.Products.FindAsync(id);
            if (product == null) return null;

            var skuNorm = sku.ToLowerInvariant();
            if (await _context.Products.AnyAsync(p => p.Sku.ToLower() == skuNorm && p.Id != id))
                throw new InvalidOperationException("SKU already exists");

            var barcode = NormalizeBarcode(request.Barcode);
            if (barcode != null)
            {
                barcode = InputValidator.SanitizeString(barcode, 64);
                var bcNorm = barcode.ToLowerInvariant();
                if (await _context.Products.AnyAsync(p => p.Barcode != null && p.Barcode.ToLower() == bcNorm && p.Id != id))
                    throw new InvalidOperationException("Barcode already exists");
            }

            if (product.SellPrice != request.SellPrice && userId.HasValue)
            {
                var priceChange = request.SellPrice - product.SellPrice;
                var percentageChange = product.SellPrice > 0 ? (priceChange / product.SellPrice) * 100 : 0;

                var priceLog = new PriceChangeLog
                {
                    ProductId = id,
                    OldPrice = product.SellPrice,
                    NewPrice = request.SellPrice,
                    PriceDifference = percentageChange,
                    ChangedBy = userId.Value,
                    Reason = "Product price updated",
                    ChangedAt = DateTime.UtcNow
                };

                _context.PriceChangeLogs.Add(priceLog);

                if (Math.Abs(percentageChange) > 10)
                    Console.WriteLine($"⚠️ PRICE ALERT: Product {product.NameEn} price changed by {percentageChange:F2}% (was {product.SellPrice:C}, now {request.SellPrice:C})");
            }

            product.Sku = InputValidator.SanitizeString(sku, 100);
            product.Barcode = barcode == null ? null : InputValidator.SanitizeString(barcode, 64);
            product.NameEn = InputValidator.SanitizeString(request.NameEn.Trim(), 200);
            product.NameAr = InputValidator.SanitizeString(request.NameAr, 200);
            product.UnitType = InputValidator.SanitizeString(request.UnitType, 50);
            product.ConversionToBase = request.ConversionToBase > 0 ? request.ConversionToBase : product.ConversionToBase;
            product.CostPrice = request.CostPrice >= 0 ? request.CostPrice : product.CostPrice;
            product.SellPrice = request.SellPrice >= 0 ? request.SellPrice : product.SellPrice;
            product.StockQty = request.StockQty >= 0 ? request.StockQty : product.StockQty;
            product.ReorderLevel = request.ReorderLevel >= 0 ? request.ReorderLevel : product.ReorderLevel;
            product.DescriptionEn = InputValidator.SanitizeString(request.DescriptionEn, 1000);
            product.DescriptionAr = InputValidator.SanitizeString(request.DescriptionAr, 1000);
            product.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return MapToDto(product);
        }

        public async Task<bool> DeleteProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return false;

            product.IsActive = false;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return true;
        }

        public async Task<ProductDto?> ReactivateProductAsync(int id)
        {
            var product = await _context.Products.FindAsync(id);
            if (product == null) return null;

            product.IsActive = true;
            product.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return MapToDto(product);
        }

        public async Task<bool> AdjustStockAsync(int productId, decimal changeQty, string reason, int userId)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var product = await _inventoryLedger.LoadProductForStockUpdateAsync(productId);
                if (product == null) return false;

                product.StockQty += changeQty;
                product.UpdatedAt = DateTime.UtcNow;

                var inventoryTransaction = new InventoryTransaction
                {
                    ProductId = productId,
                    ChangeQty = changeQty,
                    TransactionType = TransactionType.Adjustment,
                    Reason = reason,
                    CreatedAt = DateTime.UtcNow
                };

                _context.InventoryTransactions.Add(inventoryTransaction);

                var auditLog = new AuditLog
                {
                    UserId = userId,
                    Action = "Stock Adjustment",
                    Details = $"Product: {product.NameEn}, Change: {changeQty}, Reason: {reason}",
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
                return false;
            }
        }

        public async Task<List<ProductDto>> GetLowStockProductsAsync()
        {
            var products = await _context.Products
                .Where(p => p.IsActive && p.StockQty <= p.ReorderLevel)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Barcode = p.Barcode,
                    IsActive = p.IsActive,
                    NameEn = p.NameEn,
                    NameAr = p.NameAr,
                    UnitType = p.UnitType,
                    ConversionToBase = p.ConversionToBase,
                    CostPrice = p.CostPrice,
                    SellPrice = p.SellPrice,
                    StockQty = p.StockQty,
                    ReorderLevel = p.ReorderLevel,
                    ExpiryDate = p.ExpiryDate,
                    DescriptionEn = p.DescriptionEn,
                    DescriptionAr = p.DescriptionAr
                })
                .ToListAsync();

            return products.OrderBy(p => p.StockQty).ToList();
        }

        public async Task<List<ProductDto>> SearchProductsAsync(string? query, int limit = 50, bool includeInactive = false)
        {
            limit = Math.Clamp(limit, 1, 200);
            var q = ProductSearchHelper.NormalizeQuery(query);
            var baseQ = BaseQuery(includeInactive);

            if (string.IsNullOrEmpty(q))
            {
                return await baseQ.OrderBy(p => p.NameEn).Take(limit)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Sku = p.Sku,
                        Barcode = p.Barcode,
                        IsActive = p.IsActive,
                        NameEn = p.NameEn,
                        NameAr = p.NameAr,
                        UnitType = p.UnitType,
                        ConversionToBase = p.ConversionToBase,
                        CostPrice = p.CostPrice,
                        SellPrice = p.SellPrice,
                        StockQty = p.StockQty,
                        ReorderLevel = p.ReorderLevel,
                        ExpiryDate = p.ExpiryDate,
                        DescriptionEn = p.DescriptionEn,
                        DescriptionAr = p.DescriptionAr
                    })
                    .ToListAsync();
            }

            var filtered = ApplyIlikeSearch(baseQ, q);
            var list = await filtered.OrderBy(p => p.NameEn).Take(limit)
                .Select(p => new ProductDto
                {
                    Id = p.Id,
                    Sku = p.Sku,
                    Barcode = p.Barcode,
                    IsActive = p.IsActive,
                    NameEn = p.NameEn,
                    NameAr = p.NameAr,
                    UnitType = p.UnitType,
                    ConversionToBase = p.ConversionToBase,
                    CostPrice = p.CostPrice,
                    SellPrice = p.SellPrice,
                    StockQty = p.StockQty,
                    ReorderLevel = p.ReorderLevel,
                    ExpiryDate = p.ExpiryDate,
                    DescriptionEn = p.DescriptionEn,
                    DescriptionAr = p.DescriptionAr
                })
                .ToListAsync();

            if (list.Count > 0) return list;

            // Fuzzy fallback: OR match on significant tokens (extra spaces / partial words)
            var tokens = q.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            if (tokens.Length <= 1) return list;

            var ids = new HashSet<int>();
            var merged = new List<ProductDto>();
            foreach (var token in tokens.Take(8))
            {
                if (token.Length < 2) continue;
                var tp = ProductSearchHelper.ToContainsPattern(token);
                var batch = await baseQ.Where(p =>
                        EF.Functions.ILike(p.NameEn, tp) ||
                        (p.NameAr != null && EF.Functions.ILike(p.NameAr, tp)) ||
                        EF.Functions.ILike(p.Sku, tp) ||
                        (p.Barcode != null && EF.Functions.ILike(p.Barcode, tp)))
                    .OrderBy(p => p.NameEn).Take(limit)
                    .Select(p => new ProductDto
                    {
                        Id = p.Id,
                        Sku = p.Sku,
                        Barcode = p.Barcode,
                        IsActive = p.IsActive,
                        NameEn = p.NameEn,
                        NameAr = p.NameAr,
                        UnitType = p.UnitType,
                        ConversionToBase = p.ConversionToBase,
                        CostPrice = p.CostPrice,
                        SellPrice = p.SellPrice,
                        StockQty = p.StockQty,
                        ReorderLevel = p.ReorderLevel,
                        ExpiryDate = p.ExpiryDate,
                        DescriptionEn = p.DescriptionEn,
                        DescriptionAr = p.DescriptionAr
                    })
                    .ToListAsync();

                foreach (var dto in batch)
                {
                    if (ids.Add(dto.Id)) merged.Add(dto);
                    if (merged.Count >= limit) break;
                }

                if (merged.Count >= limit) break;
            }

            return merged;
        }

        public async Task<List<DuplicateProductGroupDto>> GetDuplicateProductNameGroupsAsync(int maxGroups = 50)
        {
            maxGroups = Math.Clamp(maxGroups, 1, 200);
            var active = await _context.Products
                .Where(p => p.IsActive)
                .AsNoTracking()
                .ToListAsync();

            return active
                .GroupBy(p => (p.NameEn ?? "").Trim().ToLowerInvariant())
                .Where(g => g.Key.Length > 0 && g.Count() > 1)
                .OrderByDescending(g => g.Count())
                .Take(maxGroups)
                .Select(g => new DuplicateProductGroupDto
                {
                    NormalizedName = g.Key,
                    Products = g.OrderBy(p => p.Sku).Select(MapToDto).ToList()
                })
                .ToList();
        }

        public async Task<List<PriceChangeLogDto>> GetPriceChangeHistoryAsync(int productId)
        {
            var logs = await _context.PriceChangeLogs
                .Where(p => p.ProductId == productId)
                .OrderByDescending(p => p.ChangedAt)
                .Include(p => p.ChangedByUser)
                .Select(p => new PriceChangeLogDto
                {
                    Id = p.Id,
                    ProductId = p.ProductId,
                    OldPrice = p.OldPrice,
                    NewPrice = p.NewPrice,
                    PriceDifference = p.PriceDifference,
                    ChangedBy = p.ChangedBy,
                    ChangedByName = p.ChangedByUser != null ? p.ChangedByUser.Name : "Unknown",
                    Reason = p.Reason,
                    ChangedAt = p.ChangedAt
                })
                .ToListAsync();

            return logs;
        }

        public async Task<int> ResetAllStockAsync(int userId)
        {
            var products = await _context.Products.Where(p => p.IsActive).ToListAsync();
            var count = 0;

            foreach (var product in products)
            {
                if (product.StockQty == 0) continue;

                var adjustment = new InventoryTransaction
                {
                    ProductId = product.Id,
                    ChangeQty = -product.StockQty,
                    TransactionType = TransactionType.Adjustment,
                    Reason = "Admin stock reset - All stock reset to zero",
                    CreatedAt = DateTime.UtcNow
                };
                _context.InventoryTransactions.Add(adjustment);

                product.StockQty = 0;
                product.UpdatedAt = DateTime.UtcNow;
                count++;
            }

            await _context.SaveChangesAsync();
            return count;
        }
    }
}
