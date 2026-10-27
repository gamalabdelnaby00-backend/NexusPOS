namespace UniversalPOS.Domain.Entities;

public enum StockAdjustmentStatus { Draft = 0, Approved = 1, Cancelled = 2 }

public class StockAdjustment
{
    private readonly List<StockAdjustmentItem> _items = new();
    public int Id { get; private set; }
    public string Reason { get; private set; } = string.Empty;
    public StockAdjustmentStatus Status { get; private set; } = StockAdjustmentStatus.Draft;
    public int CreatedByUserId { get; private set; }
    public int? ApprovedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;
    public DateTime? ApprovedAtUtc { get; private set; }
    public IReadOnlyCollection<StockAdjustmentItem> Items => _items.AsReadOnly();
    private StockAdjustment() { }
    public StockAdjustment(string reason, int createdByUserId)
    {
        if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Reason is required.", nameof(reason));
        if (createdByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(createdByUserId));
        Reason = reason.Trim(); CreatedByUserId = createdByUserId;
    }
    public void AddItem(StockAdjustmentItem item)
    {
        if (Status != StockAdjustmentStatus.Draft) throw new InvalidOperationException("Only draft adjustments can be edited.");
        ArgumentNullException.ThrowIfNull(item);
        if (_items.Any(x => x.ProductId == item.ProductId)) throw new InvalidOperationException("A product can appear only once in an adjustment.");
        _items.Add(item);
    }
    public void Approve(int approverUserId)
    {
        if (Status != StockAdjustmentStatus.Draft) throw new InvalidOperationException("Only draft adjustments can be approved.");
        if (approverUserId <= 0) throw new ArgumentOutOfRangeException(nameof(approverUserId));
        if (_items.Count == 0) throw new InvalidOperationException("An adjustment must contain at least one item.");
        ApprovedByUserId = approverUserId; ApprovedAtUtc = DateTime.UtcNow; Status = StockAdjustmentStatus.Approved;
    }
    public void Cancel()
    {
        if (Status != StockAdjustmentStatus.Draft) throw new InvalidOperationException("Only draft adjustments can be cancelled.");
        Status = StockAdjustmentStatus.Cancelled;
    }
}
