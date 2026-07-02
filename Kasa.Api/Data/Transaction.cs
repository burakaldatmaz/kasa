using Kasa.Domain;

namespace Kasa.Api.Data;

/// <summary>İşlem persistence modeli. Para her zaman satang (I1).</summary>
public class Transaction
{
    public int Id { get; set; }
    public DateOnly Date { get; set; }
    public TransactionType Type { get; set; }
    public int CategoryId { get; set; }
    public Category? Category { get; set; }
    public PaymentMethod PaymentMethod { get; set; }
    public long AmountSatang { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}
