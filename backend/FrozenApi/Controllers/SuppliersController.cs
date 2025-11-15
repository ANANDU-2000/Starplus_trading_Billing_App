/*
Purpose: Suppliers controller for supplier ledger
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
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService;
        }

        [HttpGet("balance/{supplierName}")]
        public async Task<ActionResult<ApiResponse<SupplierBalanceDto>>> GetSupplierBalance(string supplierName)
        {
            try
            {
                var result = await _supplierService.GetSupplierBalanceAsync(supplierName);
                return Ok(new ApiResponse<SupplierBalanceDto>
                {
                    Success = true,
                    Message = "Supplier balance retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<SupplierBalanceDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("transactions/{supplierName}")]
        public async Task<ActionResult<ApiResponse<List<SupplierTransactionDto>>>> GetSupplierTransactions(
            string supplierName,
            [FromQuery] DateTime? fromDate = null,
            [FromQuery] DateTime? toDate = null)
        {
            try
            {
                var result = await _supplierService.GetSupplierTransactionsAsync(supplierName, fromDate, toDate);
                return Ok(new ApiResponse<List<SupplierTransactionDto>>
                {
                    Success = true,
                    Message = "Supplier transactions retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<SupplierTransactionDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("summary")]
        public async Task<ActionResult<ApiResponse<List<SupplierSummaryDto>>>> GetAllSuppliersSummary()
        {
            try
            {
                var result = await _supplierService.GetAllSuppliersSummaryAsync();
                return Ok(new ApiResponse<List<SupplierSummaryDto>>
                {
                    Success = true,
                    Message = "Suppliers summary retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<SupplierSummaryDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }
    }
}

