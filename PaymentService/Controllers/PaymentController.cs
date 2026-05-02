using Microsoft.AspNetCore.Mvc;
using PaymentService.DTOs;
using PaymentService.Services;
using PaymentService.Attributes;

namespace PaymentService.Controllers;

[ApiController]
[Route("api/v1/payments")]
[RequireUserType("paymentGateway")]
public class PaymentController(IPaymentProcessor paymentProcessor) : ControllerBase
{
    [HttpPost("charge")]
    public async Task<IActionResult> Charge(
        [FromHeader(Name = "Idempotency-Key")] string idempotencyKey,
        [FromBody] PaymentRequest request,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return BadRequest(new { Message = "Idempotency-Key header is required." });
        }

        try
        {
            var payment = await paymentProcessor.ProcessPaymentAsync(request, idempotencyKey, cancellationToken);
            
            var response = new PaymentResponse(
                payment.PaymentId,
                payment.BillId,
                payment.Amount,
                payment.Status,
                payment.Reference
            );

            return CreatedAtAction(nameof(GetPaymentById), new { id = payment.PaymentId }, response);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { Message = ex.Message });
        }
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> GetPaymentById(int id, CancellationToken cancellationToken)
    {
        var payment = await paymentProcessor.GetPaymentByIdAsync(id, cancellationToken);
        
        if (payment == null) 
        {
            return NotFound();
        }

        return Ok(payment);
    }

    [HttpGet("bill/{billId}")]
    public async Task<IActionResult> GetPaymentsByBillId(int billId, CancellationToken cancellationToken)
    {
        var payments = await paymentProcessor.GetPaymentsByBillIdAsync(billId, cancellationToken);
        return Ok(payments);
    }
}
