using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities;

public class StockMovement
{
    public int Id { get; private set; }
    public int ProductId { get; private set; }
    public StockMovementType Type { get; private set; }
    public decimal QuantityChange { get; private set; }
    public string ReferenceType { get; private set; } = string.Empty;
    public string ReferenceId { get; private set; } = string.Empty;
    public string Reason { get; private set; } = string.Empty;
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public int CreatedByUserId { get; private set; }

    private StockMovement() { }

    public StockMovement(int productId,StockMovementType type,decimal quantityChange,string referenceType,string referenceId,string reason,int createdByUserId)
    {
        if (productId <= 0 || createdByUserId <= 0)
            throw new ArgumentOutOfRangeException("IDs must be positive.");

        if (quantityChange == 0)
            throw new ArgumentOutOfRangeException(nameof(quantityChange));

        if (string.IsNullOrWhiteSpace(referenceType) || string.IsNullOrWhiteSpace(referenceId) || string.IsNullOrWhiteSpace(reason))
        {
            throw new ArgumentException("Reference and reason are required.");
        }

        ProductId = productId;
        Type = type;
        QuantityChange = quantityChange;
        ReferenceType = referenceType.Trim();
        ReferenceId = referenceId.Trim();
        Reason = reason.Trim();
        CreatedByUserId = createdByUserId;
    }
}
