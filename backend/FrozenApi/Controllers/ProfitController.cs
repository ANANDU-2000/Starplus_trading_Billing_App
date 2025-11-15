/*
Purpose: Profit controller for profit calculations and reports
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
    [Authorize]
    public class ProfitController : ControllerBase
    {
        private readonly IProfitService _profitService;

        public ProfitController(IProfitService profitService)
        {
            _profitService = profitService;
        }

        [HttpGet("report")]
        public async Task<ActionResult<ApiResponse<ProfitReportDto>>> GetProfitReport(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-30);
                var to = toDate ?? DateTime.UtcNow.Date;
                var result = await _profitService.CalculateProfitAsync(from, to);
                return Ok(new ApiResponse<ProfitReportDto>
                {
                    Success = true,
                    Message = "Profit report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<ProfitReportDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("products")]
        public async Task<ActionResult<ApiResponse<List<ProductProfitDto>>>> GetProductProfit(
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var from = fromDate ?? DateTime.UtcNow.Date.AddDays(-30);
                var to = toDate ?? DateTime.UtcNow.Date;
                var result = await _profitService.CalculateProductProfitAsync(from, to);
                return Ok(new ApiResponse<List<ProductProfitDto>>
                {
                    Success = true,
                    Message = "Product profit report generated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<ProductProfitDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("daily")]
        public async Task<ActionResult<ApiResponse<DailyProfitDto>>> GetDailyProfit([FromQuery] DateTime? date = null)
        {
            try
            {
                var targetDate = date ?? DateTime.UtcNow.Date;
                var result = await _profitService.GetDailyProfitAsync(targetDate);
                return Ok(new ApiResponse<DailyProfitDto>
                {
                    Success = true,
                    Message = "Daily profit retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<DailyProfitDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

