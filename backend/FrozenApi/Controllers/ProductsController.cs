/*
Purpose: Products controller for inventory management
Author: AI Assistant
Date: 2024
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly IExcelImportService _excelImportService;

        public ProductsController(IProductService productService, IExcelImportService excelImportService)
        {
            _productService = productService;
            _excelImportService = excelImportService;
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<PagedResponse<ProductDto>>>> GetProducts(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? search = null,
            [FromQuery] bool lowStock = false,
            [FromQuery] string? unitType = null)
        {
            try
            {
                var result = await _productService.GetProductsAsync(page, pageSize, search, lowStock, unitType);
                return Ok(new ApiResponse<PagedResponse<ProductDto>>
                {
                    Success = true,
                    Message = "Products retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PagedResponse<ProductDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<ProductDto>>> GetProduct(int id)
        {
            try
            {
                var result = await _productService.GetProductByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new ApiResponse<ProductDto>
                    {
                        Success = false,
                        Message = "Product not found"
                    });
                }

                return Ok(new ApiResponse<ProductDto>
                {
                    Success = true,
                    Message = "Product retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ProductDto>>> CreateProduct([FromBody] CreateProductRequest request)
        {
            try
            {
                var result = await _productService.CreateProductAsync(request);
                return CreatedAtAction(nameof(GetProduct), new { id = result.Id }, new ApiResponse<ProductDto>
                {
                    Success = true,
                    Message = "Product created successfully",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Microsoft.EntityFrameworkCore.DbUpdateException ex)
            {
                var errorMessage = ex.InnerException?.Message ?? ex.Message;
                Console.WriteLine($"❌ Database Error in CreateProduct: {errorMessage}");
                Console.WriteLine($"❌ Full Exception: {ex}");
                return StatusCode(500, new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = "Database error occurred while creating product. Please check database schema.",
                    Errors = new List<string> { errorMessage }
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"❌ CreateProduct Error: {ex.Message}");
                Console.WriteLine($"❌ Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"❌ Inner Exception: {ex.InnerException.Message}");
                }
                return StatusCode(500, new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = $"An error occurred: {ex.Message}",
                    Errors = new List<string> { ex.Message, ex.InnerException?.Message ?? "" }
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<ProductDto>>> UpdateProduct(int id, [FromBody] CreateProductRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                int? userId = userIdClaim != null && int.TryParse(userIdClaim.Value, out int uid) ? uid : null;
                
                var result = await _productService.UpdateProductAsync(id, request, userId);
                if (result == null)
                {
                    return NotFound(new ApiResponse<ProductDto>
                    {
                        Success = false,
                        Message = "Product not found"
                    });
                }

                return Ok(new ApiResponse<ProductDto>
                {
                    Success = true,
                    Message = "Product updated successfully",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return Conflict(new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ProductDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteProduct(int id)
        {
            try
            {
                var result = await _productService.DeleteProductAsync(id);
                if (!result)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Product deleted successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("{id}/adjust-stock")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> AdjustStock(int id, [FromBody] StockAdjustmentRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _productService.AdjustStockAsync(id, request.ChangeQty, request.Reason, userId);
                if (!result)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Product not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Stock adjusted successfully"
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("low-stock")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductDto>>>> GetLowStockProducts()
        {
            try
            {
                var result = await _productService.GetLowStockProductsAsync();
                return Ok(new ApiResponse<List<ProductDto>>
                {
                    Success = true,
                    Message = "Low stock products retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ProductDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("search")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<ProductDto>>>> SearchProducts(
            [FromQuery] string q,
            [FromQuery] int limit = 20)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(q))
                {
                    return BadRequest(new ApiResponse<List<ProductDto>>
                    {
                        Success = false,
                        Message = "Search query is required"
                    });
                }

                var result = await _productService.SearchProductsAsync(q, limit);
                return Ok(new ApiResponse<List<ProductDto>>
                {
                    Success = true,
                    Message = "Products retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ProductDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}/price-history")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<List<PriceChangeLogDto>>>> GetPriceHistory(int id)
        {
            try
            {
                var result = await _productService.GetPriceChangeHistoryAsync(id);
                return Ok(new ApiResponse<List<PriceChangeLogDto>>
                {
                    Success = true,
                    Message = "Price history retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<PriceChangeLogDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("import-excel")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ExcelImportResult>>> ImportProductsFromExcel(IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiResponse<ExcelImportResult>
                    {
                        Success = false,
                        Message = "No file uploaded"
                    });
                }

                if (!file.FileName.EndsWith(".xlsx", StringComparison.OrdinalIgnoreCase) &&
                    !file.FileName.EndsWith(".xls", StringComparison.OrdinalIgnoreCase))
                {
                    return BadRequest(new ApiResponse<ExcelImportResult>
                    {
                        Success = false,
                        Message = "Invalid file type. Please upload an Excel file (.xlsx or .xls)"
                    });
                }

                var userIdClaim = User.FindFirst("UserId") ?? 
                                  User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) ?? 
                                  User.FindFirst("id");
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId) || userId == 0)
                {
                    return Unauthorized(new ApiResponse<ExcelImportResult>
                    {
                        Success = false,
                        Message = "Invalid user authentication"
                    });
                }

                using var stream = file.OpenReadStream();
                var result = await _excelImportService.ImportProductsFromExcelAsync(stream, file.FileName, userId);

                return Ok(new ApiResponse<ExcelImportResult>
                {
                    Success = true,
                    Message = $"Import completed: {result.Imported} new, {result.Updated} updated, {result.Skipped} skipped, {result.Errors} errors",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ExcelImportResult>
                {
                    Success = false,
                    Message = "An error occurred during import",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("reset-all-stock")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> ResetAllStock()
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _productService.ResetAllStockAsync(userId);
                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Stock reset successfully for {result} products",
                    Data = new { ProductsUpdated = result }
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<object>
                {
                    Success = false,
                    Message = "An error occurred while resetting stock",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }

    public class StockAdjustmentRequest
    {
        public decimal ChangeQty { get; set; }
        public string Reason { get; set; } = string.Empty;
    }
}

