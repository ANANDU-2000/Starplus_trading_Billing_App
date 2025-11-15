/*
Purpose: Payments controller for payment tracking
Author: AI Assistant
Date: 2024
Updated: 2025 - Complete rewrite per spec for proper payment/invoice/balance tracking
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
    public class PaymentsController : ControllerBase
    {
        private readonly IPaymentService _paymentService;

        public PaymentsController(IPaymentService paymentService)
        {
            _paymentService = paymentService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<PagedResponse<PaymentDto>>>> GetPayments(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 10)
        {
            try
            {
                var result = await _paymentService.GetPaymentsAsync(page, pageSize);
                return Ok(new ApiResponse<PagedResponse<PaymentDto>>
                {
                    Success = true,
                    Message = "Payments retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PagedResponse<PaymentDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> GetPayment(int id)
        {
            try
            {
                var result = await _paymentService.GetPaymentByIdAsync(id);
                if (result == null)
                {
                    return NotFound(new ApiResponse<PaymentDto>
                    {
                        Success = false,
                        Message = "Payment not found"
                    });
                }

                return Ok(new ApiResponse<PaymentDto>
                {
                    Success = true,
                    Message = "Payment retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PaymentDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<CreatePaymentResponse>>> CreatePayment(
            [FromBody] CreatePaymentRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("UserId") 
                    ?? User.FindFirst("sub");
                
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<CreatePaymentResponse>
                    {
                        Success = false,
                        Message = "Invalid user authentication"
                    });
                }

                // Generate idempotency key if not provided (for safety)
                if (string.IsNullOrEmpty(idempotencyKey))
                {
                    idempotencyKey = Guid.NewGuid().ToString();
                }

                var result = await _paymentService.CreatePaymentAsync(request, userId, idempotencyKey);
                return CreatedAtAction(nameof(GetPayment), new { id = result.Payment.Id }, new ApiResponse<CreatePaymentResponse>
                {
                    Success = true,
                    Message = "Payment created successfully",
                    Data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<CreatePaymentResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (InvalidOperationException ex)
            {
                // Handle concurrency conflicts and validation errors
                if (ex.Message.Contains("modified by another user") || ex.Message.Contains("CONFLICT"))
                {
                    return StatusCode(409, new ApiResponse<CreatePaymentResponse>
                    {
                        Success = false,
                        Message = ex.Message
                    });
                }
                return BadRequest(new ApiResponse<CreatePaymentResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<CreatePaymentResponse>
                {
                    Success = false,
                    Message = "An error occurred while creating payment",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("{id}/status")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> UpdatePaymentStatus(int id, [FromBody] PaymentStatusRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("UserId")
                    ?? User.FindFirst("sub");
                
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                if (!Enum.TryParse<PaymentStatus>(request.Status, out var status))
                {
                    return BadRequest(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid payment status. Must be PENDING, CLEARED, RETURNED, or VOID"
                    });
                }

                var result = await _paymentService.UpdatePaymentStatusAsync(id, status, userId);
                if (!result)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Payment not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = $"Payment status updated to {request.Status}"
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

        [HttpGet("customers/{customerId}/outstanding-invoices")]
        public async Task<ActionResult<ApiResponse<List<Models.OutstandingInvoiceDto>>>> GetOutstandingInvoices(int customerId)
        {
            try
            {
                var result = await _paymentService.GetOutstandingInvoicesAsync(customerId);
                return Ok(new ApiResponse<List<Models.OutstandingInvoiceDto>>
                {
                    Success = true,
                    Message = "Outstanding invoices retrieved successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<List<OutstandingInvoiceDto>>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpGet("invoices/{invoiceId}/amount")]
        public async Task<ActionResult<ApiResponse<InvoiceAmountDto>>> GetInvoiceAmount(int invoiceId)
        {
            try
            {
                var result = await _paymentService.GetInvoiceAmountAsync(invoiceId);
                return Ok(new ApiResponse<InvoiceAmountDto>
                {
                    Success = true,
                    Message = "Invoice amount retrieved successfully",
                    Data = result
                });
            }
            catch (ArgumentException ex)
            {
                return NotFound(new ApiResponse<InvoiceAmountDto>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<InvoiceAmountDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPost("allocate")]
        public async Task<ActionResult<ApiResponse<CreatePaymentResponse>>> AllocatePayment(
            [FromBody] AllocatePaymentRequest request,
            [FromHeader(Name = "Idempotency-Key")] string? idempotencyKey = null)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier) 
                    ?? User.FindFirst("UserId") 
                    ?? User.FindFirst("sub");
                
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<CreatePaymentResponse>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                // Generate idempotency key if not provided
                if (string.IsNullOrEmpty(idempotencyKey))
                {
                    idempotencyKey = Guid.NewGuid().ToString();
                }

                var result = await _paymentService.AllocatePaymentAsync(request, userId, idempotencyKey);
                return Ok(new ApiResponse<CreatePaymentResponse>
                {
                    Success = true,
                    Message = "Payment allocated successfully",
                    Data = result
                });
            }
            catch (ArgumentException ex)
            {
                return BadRequest(new ApiResponse<CreatePaymentResponse>
                {
                    Success = false,
                    Message = ex.Message
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<CreatePaymentResponse>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpPut("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<PaymentDto>>> UpdatePayment(int id, [FromBody] Services.UpdatePaymentRequest request)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("UserId")
                    ?? User.FindFirst("sub");
                
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<PaymentDto>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _paymentService.UpdatePaymentAsync(id, request, userId);
                if (result == null)
                {
                    return NotFound(new ApiResponse<PaymentDto>
                    {
                        Success = false,
                        Message = "Payment not found"
                    });
                }

                return Ok(new ApiResponse<PaymentDto>
                {
                    Success = true,
                    Message = "Payment updated successfully",
                    Data = result
                });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new ApiResponse<PaymentDto>
                {
                    Success = false,
                    Message = "An error occurred",
                    Errors = new List<string> { ex.Message }
                });
            }
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        public async Task<ActionResult<ApiResponse<object>>> DeletePayment(int id)
        {
            try
            {
                var userIdClaim = User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)
                    ?? User.FindFirst("UserId")
                    ?? User.FindFirst("sub");
                
                if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
                {
                    return Unauthorized(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Invalid user"
                    });
                }

                var result = await _paymentService.DeletePaymentAsync(id, userId);
                if (!result)
                {
                    return NotFound(new ApiResponse<object>
                    {
                        Success = false,
                        Message = "Payment not found"
                    });
                }

                return Ok(new ApiResponse<object>
                {
                    Success = true,
                    Message = "Payment deleted successfully"
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
    }

    public class PaymentStatusRequest
    {
        public string Status { get; set; } = string.Empty; // PENDING, CLEARED, RETURNED, VOID
    }
}
