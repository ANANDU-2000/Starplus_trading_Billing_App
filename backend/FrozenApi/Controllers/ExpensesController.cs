/*
Purpose: Expenses controller for expense tracking
Author: AI Assistant
Date: 2024
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;
using FrozenApi.Data;
using FrozenApi.Helpers;
using Microsoft.EntityFrameworkCore;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ExpensesController : ControllerBase
    {
        private readonly IExpenseService _expenseService;
        private readonly AppDbContext _context;

        public ExpensesController(IExpenseService expenseService, AppDbContext context)
        {
            _expenseService = expenseService;
            _context = context;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<ExpenseDto>>>> GetExpenses(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10,
            [FromQuery] string? category = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string? groupBy = null,
            [FromQuery] bool noVatOnly = false)
        {
            try
            {
                var result = await _expenseService.GetExpensesAsync(page, pageSize, category, fromDate, toDate, groupBy, noVatOnly);
                return Ok(new ApiResponse<PagedResponse<ExpenseDto>>
                {
                    Success = true,
                    Message = "Expenses retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                if (SchemaOutdatedHelper.IsSchemaOutdated(ex))
                    return StatusCode(503, new ApiResponse<PagedResponse<ExpenseDto>> { Success = false, Message = SchemaOutdatedHelper.SchemaOutdatedMessage });
                return StatusCode(500, new ApiResponse<PagedResponse<ExpenseDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("aggregated")]
        public async Task<ActionResult<ApiResponse<List<ExpenseAggregateDto>>>> GetExpensesAggregated(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null,
            [FromQuery] string groupBy = "monthly") // weekly, monthly, yearly
        {
            try
            {
                var from = fromDate ?? DateTime.UtcNow.Date.AddMonths(-6);
                var to = toDate ?? DateTime.UtcNow.Date;
                var result = await _expenseService.GetExpensesAggregatedAsync(from, to, groupBy);
                return Ok(new ApiResponse<List<ExpenseAggregateDto>>
                {
                    Success = true,
                    Message = "Aggregated expenses retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ExpenseAggregateDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<ExpenseDto>>> GetExpense(int id)
        {
            try
            {
                var result = await _expenseService.GetExpenseByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new ApiResponse<ExpenseDto>
                    {
                        Success = false,
                        Message = "Expense not found"
                    });
                }

                return Ok(new ApiResponse<ExpenseDto>
                {
                    Success = true,
                    Message = "Expense retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ExpenseDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<ExpenseDto>>> CreateExpense([FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<ExpenseDto>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _expenseService.CreateExpenseAsync(request, userId);
                return CreatedAtAction(nameof(GetExpense), new { id = result.Id }, new ApiResponse<ExpenseDto>
                {
                    Success = true,
                    Message = "Expense created successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ExpenseDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("{id}")]
        public async Task<ActionResult<ApiResponse<ExpenseDto>>> UpdateExpense(int id, [FromBody] CreateExpenseRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<ExpenseDto>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _expenseService.UpdateExpenseAsync(id, request, userId);
                if (result == null)
                {
                    return NotFound(new ApiResponse<ExpenseDto>
                    {
                        Success = false,
                        Message = "Expense not found"
                    });
                }

                return Ok(new ApiResponse<ExpenseDto>
                {
                    Success = true,
                    Message = "Expense updated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ExpenseDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeleteExpense(int id)
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

                var result = await _expenseService.DeleteExpenseAsync(id, userId);
                if (!result)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Expense not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Expense deleted successfully"
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

        [HttpPost("bulk-vat-update")]
        public async Task<ActionResult<ApiResponse<BulkVatUpdateResult>>> BulkVatUpdate([FromBody] BulkVatUpdateRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier);
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<BulkVatUpdateResult>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }
                var result = await _expenseService.BulkVatUpdateAsync(request, userId);
                return Ok(new ApiResponse<BulkVatUpdateResult>
                {
                    Success = true,
                    Message = $"Bulk VAT update completed. Updated: {result.Updated}, Skipped: {result.Skipped}",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<BulkVatUpdateResult>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("categories")]
        public async Task<ActionResult<ApiResponse<List<ExpenseCategoryDto>>>> GetCategories()
        {
            try
            {
                var categories = await _context.ExpenseCategories
                    .Where(c => c.IsActive)
                    .OrderBy(c => c.Name)
                    .Select(c => new ExpenseCategoryDto
                    {
                        Id = c.Id,
                        Name = c.Name,
                        ColorCode = c.ColorCode,
                        DefaultVatRate = c.DefaultVatRate,
                        DefaultTaxType = c.DefaultTaxType,
                        DefaultIsTaxClaimable = c.DefaultIsTaxClaimable,
                        DefaultIsEntertainment = c.DefaultIsEntertainment,
                        VatDefaultLocked = c.VatDefaultLocked
                    })
                    .ToListAsync();

                return Ok(new ApiResponse<List<ExpenseCategoryDto>>
                {
                    Success = true,
                    Message = "Categories retrieved successfully",
                    Data = categories
                });
            }
            catch (Exception ex)
            {
                if (SchemaOutdatedHelper.IsSchemaOutdated(ex))
                    return StatusCode(503, new ApiResponse<List<ExpenseCategoryDto>> { Success = false, Message = SchemaOutdatedHelper.SchemaOutdatedMessage });
                return StatusCode(500, new ApiResponse<List<ExpenseCategoryDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("categories")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ExpenseCategoryDto>>> CreateCategory([FromBody] CreateExpenseCategoryRequest request)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(request.Name))
                {
                    return BadRequest(new ApiResponse<ExpenseCategoryDto>
                    {
                        Success = false,
                        Message = "Category name is required",
                        Errors = new List<string> { "Name cannot be empty" }
                    });
                }

                // Check if category with same name already exists
                var existingCategory = await _context.ExpenseCategories
                    .FirstOrDefaultAsync(c => c.Name.ToLower() == request.Name.ToLower());
                
                if (existingCategory != null)
                {
                    return BadRequest(new ApiResponse<ExpenseCategoryDto>
                    {
                        Success = false,
                        Message = "Category already exists",
                        Errors = new List<string> { $"A category named '{request.Name}' already exists" }
                    });
                }

                // Validate color code
                var colorCode = request.ColorCode;
                if (string.IsNullOrWhiteSpace(colorCode) || !colorCode.StartsWith("#"))
                {
                    colorCode = "#3B82F6"; // Default blue
                }

                var category = new ExpenseCategory
                {
                    Name = request.Name.Trim(),
                    ColorCode = colorCode,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                _context.ExpenseCategories.Add(category);
                await _context.SaveChangesAsync();

                var categoryDto = new ExpenseCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    ColorCode = category.ColorCode,
                    DefaultVatRate = category.DefaultVatRate,
                    DefaultTaxType = category.DefaultTaxType,
                    DefaultIsTaxClaimable = category.DefaultIsTaxClaimable,
                    DefaultIsEntertainment = category.DefaultIsEntertainment,
                    VatDefaultLocked = category.VatDefaultLocked
                };

                return Ok(new ApiResponse<ExpenseCategoryDto>
                {
                    Success = true,
                    Message = "Category created successfully",
                    Data = categoryDto
                });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error creating expense category: {ex.Message}");
                return StatusCode(500, new ApiResponse<ExpenseCategoryDto>
                {
                    Success = false,
                    Message = "An error occurred while creating the category",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("categories/{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<ExpenseCategoryDto>>> UpdateCategory(int id, [FromBody] UpdateExpenseCategoryRequest request)
        {
            try
            {
                var category = await _context.ExpenseCategories.FindAsync(id);
                if (category == null)
                    return NotFound(new ApiResponse<ExpenseCategoryDto> { Success = false, Message = "Category not found" });
                if (request.DefaultVatRate.HasValue) category.DefaultVatRate = request.DefaultVatRate.Value;
                if (request.DefaultTaxType != null) category.DefaultTaxType = request.DefaultTaxType;
                if (request.DefaultIsTaxClaimable.HasValue) category.DefaultIsTaxClaimable = request.DefaultIsTaxClaimable.Value;
                if (request.DefaultIsEntertainment.HasValue) category.DefaultIsEntertainment = request.DefaultIsEntertainment.Value;
                if (request.VatDefaultLocked.HasValue) category.VatDefaultLocked = request.VatDefaultLocked.Value;
                await _context.SaveChangesAsync();
                var dto = new ExpenseCategoryDto
                {
                    Id = category.Id,
                    Name = category.Name,
                    ColorCode = category.ColorCode,
                    DefaultVatRate = category.DefaultVatRate,
                    DefaultTaxType = category.DefaultTaxType,
                    DefaultIsTaxClaimable = category.DefaultIsTaxClaimable,
                    DefaultIsEntertainment = category.DefaultIsEntertainment,
                    VatDefaultLocked = category.VatDefaultLocked
                };
                return Ok(new ApiResponse<ExpenseCategoryDto> { Success = true, Message = "Category updated", Data = dto });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ExpenseCategoryDto> { Success = false, Message = ex.Message, Errors = new List<string> { ex.Message } });
            }
        }
    }

    // Request DTO for creating expense category
    public class CreateExpenseCategoryRequest
    {
        public string Name { get; set; } = string.Empty;
        public string ColorCode { get; set; } = "#3B82F6";
    }
}

