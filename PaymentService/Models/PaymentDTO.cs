namespace PaymentService.DTOs;

public record PaymentRequest(int BillId, decimal Amount, string Method);

public record PaymentResponse(
    int PaymentId, 
    int BillId, 
    decimal Amount, 
    string Status, 
    string? Reference
);

public record BillResponse(int BillId, int PatientId, int AppointmentId, decimal Amount, string Status);
public record UpdateBillRequest(decimal Amount, string Status);
