using Microsoft.EntityFrameworkCore;
using PaymentService.DTOs;
using PaymentService.Models;
using PaymentService.DAL;
using System.Diagnostics.Metrics;

namespace PaymentService.Services;

public interface IPaymentProcessor
{
    Task<Payment> ProcessPaymentAsync(PaymentRequest request, string idempotencyKey, CancellationToken cancellationToken);
    Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken);
    Task<List<Payment>> GetPaymentsByBillIdAsync(int billId, CancellationToken cancellationToken);
}

public class PaymentProcessorService : IPaymentProcessor
{
    private readonly PaymentDbContext _dbContext;
    private readonly ILogger<PaymentProcessorService> _logger;
    private readonly Counter<int> _failedPaymentsCounter;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;

    public PaymentProcessorService(
        PaymentDbContext dbContext, 
        ILogger<PaymentProcessorService> logger, 
        IMeterFactory meterFactory,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration)
    {
        _dbContext = dbContext;
        _logger = logger;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        
        var meter = meterFactory.Create("Hospital.PaymentService");
        _failedPaymentsCounter = meter.CreateCounter<int>("payments_failed_total");
    }

    public async Task<Payment> ProcessPaymentAsync(PaymentRequest request, string idempotencyKey, CancellationToken cancellationToken)
    {
        // 1. Idempotency Check
        var existingPayment = await _dbContext.Payments
            .FirstOrDefaultAsync(p => p.IdempotencyKey == idempotencyKey, cancellationToken);

        if (existingPayment is not null)
        {
            _logger.LogInformation("Idempotent request detected. Returning existing payment for key: {IdempotencyKey}", idempotencyKey);
            return existingPayment;
        }

        // 1.5 Verify Bill with Billing Service
        var client = _httpClientFactory.CreateClient();
        var billingUrl = _configuration["BillingServiceBaseUrl"] ?? "http://localhost:5001";
        
        var getBillReq = new HttpRequestMessage(HttpMethod.Get, $"{billingUrl}/v1/bills/{request.BillId}");
        getBillReq.Headers.TryAddWithoutValidation("userType", "billing");
        
        var getBillRes = await client.SendAsync(getBillReq, cancellationToken);
        if (!getBillRes.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Could not fetch Bill ID {request.BillId}. Ensure bill exists.");
        }

        var bill = await getBillRes.Content.ReadFromJsonAsync<BillResponse>(cancellationToken: cancellationToken);
        if (bill == null) throw new InvalidOperationException("Failed to parse bill details.");

        if (bill.Status == "PAID") throw new InvalidOperationException("This bill is already paid.");
        
        if (bill.Amount != request.Amount)
        {
            throw new InvalidOperationException($"Amount mismatch. The bill amount is {bill.Amount}, but request was for {request.Amount}.");
        }

        // 2. Simulate Payment Gateway Processing
        bool isSuccess = SimulateGateway(request);

        // 3. Build and Save Payment Record
        var payment = new Payment
        {
            BillId = request.BillId,
            Amount = request.Amount,
            Method = request.Method,
            Status = isSuccess ? "SUCCESS" : "FAILED",
            Reference = Guid.NewGuid().ToString(),
            IdempotencyKey = idempotencyKey
        };

        _dbContext.Payments.Add(payment);
        await _dbContext.SaveChangesAsync(cancellationToken);

        if (!isSuccess)
        {
            _failedPaymentsCounter.Add(1);
            _logger.LogWarning("Payment failed for Bill ID: {BillId}", request.BillId);
            throw new InvalidOperationException("Payment gateway declined the transaction.");
        }

        _logger.LogInformation("Payment successful for Bill ID: {BillId}", request.BillId);
        
        // 4. Update BillingService status
        var updateReq = new HttpRequestMessage(HttpMethod.Put, $"{billingUrl}/v1/bills/{request.BillId}");
        updateReq.Headers.TryAddWithoutValidation("userType", "billing");
        var updateDto = new UpdateBillRequest(bill.Amount, "PAID");
        updateReq.Content = new StringContent(System.Text.Json.JsonSerializer.Serialize(updateDto), System.Text.Encoding.UTF8, "application/json");

        var updateRes = await client.SendAsync(updateReq, cancellationToken);
        if (!updateRes.IsSuccessStatusCode)
        {
            _logger.LogWarning("Failed to automatically update Bill ID {BillId} to PAID status in BillingService.", request.BillId);
        }
        
        return payment;
    }

    public async Task<Payment?> GetPaymentByIdAsync(int paymentId, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments.FindAsync(new object[] { paymentId }, cancellationToken);
    }

    public async Task<List<Payment>> GetPaymentsByBillIdAsync(int billId, CancellationToken cancellationToken)
    {
        return await _dbContext.Payments
            .Where(p => p.BillId == billId)
            .ToListAsync(cancellationToken);
    }

    private static bool SimulateGateway(PaymentRequest request)
    {
        // Mock gateway: fail if amount is 999.99 for testing purposes
        return request.Amount != 999.99m;
    }
}
