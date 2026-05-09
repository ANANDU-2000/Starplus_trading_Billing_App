/*
 * Centralized row-locked product reads for stock mutations (PostgreSQL FOR UPDATE).
 * All inventory-changing flows should load products through this service inside the same DB transaction.
 */
using Microsoft.EntityFrameworkCore;
using FrozenApi.Data;
using FrozenApi.Models;

namespace FrozenApi.Services
{
    public interface IInventoryLedgerService
    {
        /// <summary>Load product with row lock inside current transaction (PostgreSQL).</summary>
        Task<Product?> LoadProductForStockUpdateAsync(int productId, CancellationToken cancellationToken = default);
    }

    public class InventoryLedgerService : IInventoryLedgerService
    {
        private readonly AppDbContext _context;

        public InventoryLedgerService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<Product?> LoadProductForStockUpdateAsync(int productId, CancellationToken cancellationToken = default)
        {
            // FOR UPDATE serializes concurrent stock changes on the same product row.
            var list = await _context.Products
                .FromSqlInterpolated($"SELECT * FROM \"Products\" WHERE \"Id\" = {productId} FOR UPDATE")
                .AsTracking()
                .ToListAsync(cancellationToken);

            return list.FirstOrDefault();
        }
    }
}
