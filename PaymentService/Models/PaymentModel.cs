using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace PaymentService.Models;

[Table("payments")]
[Index(nameof(IdempotencyKey), IsUnique = true)]
public class Payment
{
    [Key]
    [Column("payment_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
    public int PaymentId { get; set; }
    
    [Column("bill_id")]
    public int BillId { get; set; }
    
    [Column("amount")]
    public decimal Amount { get; set; }
    
    [Column("method")]
    public string Method { get; set; } = string.Empty; // e.g., CREDIT_CARD, CASH
    
    [Column("reference")]
    public string? Reference { get; set; }
    
    [Column("paid_at")]
    public DateTime PaidAt { get; set; } = DateTime.UtcNow;
    
    [Column("status")]
    public string Status { get; set; } = string.Empty; // SUCCESS, FAILED, REFUNDED
    
    [Column("idempotency_key")]
    public string IdempotencyKey { get; set; } = string.Empty;
}
