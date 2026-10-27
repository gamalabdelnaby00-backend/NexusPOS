using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities;

public class SaleReturnPayment
{
    public int Id { get; private set; }
    public int SaleReturnId { get; private set; }
    public int ProcessedByUserId { get; private set; }
    public int? CashShiftId { get; private set; }
    public int? OriginalPaymentId { get; private set; }
    public PaymentMethod Method { get; private set; }
    public decimal Amount { get; private set; }
    public string? ExternalReference { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    private SaleReturnPayment() { }
    public SaleReturnPayment(int saleReturnId, int processedByUserId, PaymentMethod method, decimal amount, int? cashShiftId = null, int? originalPaymentId = null, string? externalReference = null)
    {
        if (saleReturnId <= 0 || processedByUserId <= 0) throw new ArgumentOutOfRangeException("IDs must be positive.");
        if (amount <= 0) throw new ArgumentOutOfRangeException(nameof(amount));
        if (!Enum.IsDefined(method)) throw new ArgumentException("Invalid payment method.", nameof(method));
        if (method == PaymentMethod.Cash && (!cashShiftId.HasValue || cashShiftId <= 0)) throw new ArgumentException("Cash refunds must reference a cash shift.", nameof(cashShiftId));
        if (cashShiftId.HasValue && cashShiftId <= 0) throw new ArgumentOutOfRangeException(nameof(cashShiftId));
        if (originalPaymentId.HasValue && originalPaymentId <= 0) throw new ArgumentOutOfRangeException(nameof(originalPaymentId));
        SaleReturnId = saleReturnId; ProcessedByUserId = processedByUserId; Method = method; Amount = amount;
        CashShiftId = cashShiftId; OriginalPaymentId = originalPaymentId; ExternalReference = string.IsNullOrWhiteSpace(externalReference) ? null : externalReference.Trim();
    }
}
