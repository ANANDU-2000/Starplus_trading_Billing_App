/*
Purpose: Purchases controller for supplier purchase management
Author: AI Assistant
Date: 2024
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;
using FrozenApi.Data;
using Microsoft.EntityFrameworkCore;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchaseService _purchaseService;
        private readonly AppDbContext _context;
        private readonly ILogger<PurchasesController> _logger;

        public PurchasesController(IPurchaseService purchaseService, AppDbContext context, ILogger<PurchasesController> logger)
        {
            _purchaseService = purchaseService;
            _context = context;
            _logger = logger;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<PurchaseDto>>>> GetPurchases(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _purchaseService.GetPurchasesAsync(page, pageSize);
                return Ok(new ApiResponse<PagedResponse<PurchaseDto>>
                {
                    Success = true,
                    Message = "Purchases retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PagedResponse<PurchaseDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<PurchaseDto>>> GetPurchase(int id)
        {
            try
            {
                var result = await _purchaseService.GetPurchaseByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Purchase not found"
                    });
                }

                return Ok(new ApiResponse<PurchaseDto>
                {
                    Success = true,
                    Message = "Purchase retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PurchaseDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<PurchaseDto>>> CreatePurchase([FromBody] CreatePurchaseRequest request)
        {
            try
            {
                // Validate request
                if (request == null)
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Request body is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.SupplierName))
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Supplier name is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.InvoiceNo))
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Invoice number is required"
                    });
                }

                if (request.Items == null || !request.Items.Any())
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Purchase must have at least one item"
                    });
                }

                // Validate each item
                foreach (var item in request.Items)
                {
                    if (item.ProductId <= 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Invalid product ID"
                        });
                    }

                    if (item.Qty <= 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Quantity must be greater than zero"
                        });
                    }

                    if (item.UnitCost < 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Unit cost cannot be negative"
                        });
                    }

                    if (string.IsNullOrWhiteSpace(item.UnitType))
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Unit type is required"
                        });
                    }
                }

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Invalid user authentication"
                    });
                }

                // Service already uses transaction, no need for nested transaction here
                _logger.LogInformation("📦 Creating purchase for supplier: {Supplier}, Invoice: {Invoice}", 
                    request.SupplierName, request.InvoiceNo);

                var result = await _purchaseService.CreatePurchaseAsync(request, userId);
                
                _logger.LogInformation("✅ Purchase created successfully: ID {Id}, Invoice {Invoice}", 
                    result.Id, result.InvoiceNo);

                return CreatedAtAction(nameof(GetPurchase), new { id = result.Id }, new ApiResponse<PurchaseDto>
                {
                    Success = true,
                    Message = "Purchase created successfully",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                _logger.LogWarning(ex, "⚠️ Purchase creation conflict: {Message}", ex.Message);
                return Conflict(new ApiResponse<PurchaseDto>
                {
                    Success = false,
                    Message = ex.Message,
                    Errors = new List<string> { ex.Message }
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Purchase creation error: {Message}", ex.Message);
                return StatusCode(500, new ApiResponse<PurchaseDto>
                {
                    Success = false,
                    Message = "Purchase creation failed. Please check your input and try again.",
                    Errors = new List<string> { ex.Message, ex.InnerException?.Message ?? "" }
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<PurchaseDto>>> UpdatePurchase(int id, [FromBody] CreatePurchaseRequest request)
        {
            try
            {
                // Validate request (same as create)
                if (request == null)
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Request body is required"
                    });
                }

                if (string.IsNullOrWhiteSpace(request.SupplierName))
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Supplier name is required"
                    });
                }

                if (request.Items == null || !request.Items.Any())
                {
                    return BadRequest(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Purchase must have at least one item"
                    });
                }

                // Validate each item
                foreach (var item in request.Items)
                {
                    if (item.ProductId <= 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Invalid product ID"
                        });
                    }

                    if (item.Qty <= 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Quantity must be greater than zero"
                        });
                    }

                    if (item.UnitCost < 0)
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Unit cost cannot be negative"
                        });
                    }

                    if (string.IsNullOrWhiteSpace(item.UnitType))
                    {
                        return BadRequest(new ApiResponse<PurchaseDto>
                        {
                            Success = false,
                            Message = "Unit type is required"
                        });
                    }
                }

                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Invalid user authentication"
                    });
                }

                _logger.LogInformation("📝 Updating purchase ID: {Id}", id);

                var result = await _purchaseService.UpdatePurchaseAsync(id, request, userId);
                
                if (result == null)
                {
                    return NotFound(new ApiResponse<PurchaseDto>
                    {
                        Success = false,
                        Message = "Purchase not found"
                    });
                }

                _logger.LogInformation("✅ Purchase updated successfully: ID {Id}", id);

                return Ok(new ApiResponse<PurchaseDto>
                {
                    Success = true,
                    Message = "Purchase updated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "❌ Purchase update error: {Message}", ex.Message);
                return StatusCode(500, new ApiResponse<PurchaseDto>
                {
                    Success = false,
                    Message = "Purchase update failed. Please try again.",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("{id}/upload")]
        [Authorize(Roles = "Admin,Staff")]
        public async Task<ActionResult<ApiResponse<string>>> UploadInvoice(int id, IFormFile file)
        {
            try
            {
                if (file == null || file.Length == 0)
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "No file uploaded"
                    });
                }

                var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
                if (purchase == null)
                {
                    return NotFound(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Purchase not found"
                    });
                }

                // Validate file type
                var allowedExtensions = new[] { ".pdf", ".jpg", ".jpeg", ".png" };
                var fileExtension = Path.GetExtension(file.FileName).ToLowerInvariant();
                if (!allowedExtensions.Contains(fileExtension))
                {
                    return BadRequest(new ApiResponse<string>
                    {
                        Success = false,
                        Message = "Invalid file type. Allowed: PDF, JPG, PNG"
                    });
                }

                // Create uploads directory if it doesn't exist
                var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "storage", "purchases");
                if (!Directory.Exists(uploadsDir))
                {
                    Directory.CreateDirectory(uploadsDir);
                }

                // Generate unique filename
                var fileName = $"{id}_{Guid.NewGuid()}{fileExtension}";
                var filePath = Path.Combine(uploadsDir, fileName);

                // Save file
                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await file.CopyToAsync(stream);
                }

                // Update purchase record
                var purchaseEntity = await _context.Purchases.FindAsync(id);
                if (purchaseEntity != null)
                {
                    purchaseEntity.InvoiceFileName = file.FileName;
                    purchaseEntity.InvoiceFilePath = $"storage/purchases/{fileName}";
                    await _context.SaveChangesAsync();
                }

                return Ok(new ApiResponse<string>
                {
                    Success = true,
                    Message = "Invoice uploaded successfully",
                    Data = fileName
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<string>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}/invoice")]
        [Authorize]
        public async Task<IActionResult> DownloadInvoice(int id)
        {
            try
            {
                var purchase = await _purchaseService.GetPurchaseByIdAsync(id);
                if (purchase == null)
                {
                    return NotFound();
                }

                var purchaseEntity = await _context.Purchases.FindAsync(id);
                if (purchaseEntity == null || string.IsNullOrEmpty(purchaseEntity.InvoiceFilePath))
                {
                    return NotFound();
                }

                var filePath = Path.Combine(Directory.GetCurrentDirectory(), purchaseEntity.InvoiceFilePath);
                if (!System.IO.File.Exists(filePath))
                {
                    return NotFound();
                }

                var fileBytes = await System.IO.File.ReadAllBytesAsync(filePath);
                var contentType = purchaseEntity.InvoiceFileName?.EndsWith(".pdf") == true 
                    ? "application/pdf" 
                    : "image/jpeg";

                return File(fileBytes, contentType, purchaseEntity.InvoiceFileName ?? "invoice");
            }
            catch (Exception)
            {
                return StatusCode(500);
            }
        }
    }
}

