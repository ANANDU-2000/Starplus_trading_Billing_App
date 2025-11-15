/*
Purpose: Stock adjustments controller
Author: AI Assistant
Date: 2025
*/
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using FrozenApi.Services;
using FrozenApi.Models;

namespace FrozenApi.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize(Roles = "Admin")]
    public class StockAdjustmentsController : ControllerBase
    {
        private readonly IStockAdjustmentService _adjustmentService;

        public StockAdjustmentsController(IStockAdjustmentService adjustmentService)
        {
            _adjustmentService = adjustmentService;
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<StockAdjustmentDto>>> CreateAdjustment([FromBody] CreateStockAdjustmentRequest request)
        {
            try
            {
                var userId = int.Parse(User.FindFirst("UserId")?.Value ?? "0");
                var result = await _adjustmentService.CreateAdjustmentAsync(request, userId);
                return Ok(new ApiResponse<StockAdjustmentDto>
                {
                    Success = true,
                    Message = "Stock adjustment created successfully",
                    Data = result
                });
            }
            catch (InvalidOperationException ex)
            {
                return BadRequest(new ApiResponse<StockAdjustmentDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<StockAdjustmentDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<StockAdjustmentDto>>>> GetAdjustments(
            [FromQuery] int? productId = null,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _adjustmentService.GetAdjustmentsAsync(productId, fromDate, toDate);
                return Ok(new ApiResponse<List<StockAdjustmentDto>>
                {
                    Success = true,
                    Message = "Stock adjustments retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<StockAdjustmentDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

