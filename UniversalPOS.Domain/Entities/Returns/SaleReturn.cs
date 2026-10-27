
using System;
using System.Collections.Generic;
using System.Linq;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities;

public class SaleReturn
{
    private readonly List<SaleReturnItem> _items = new();

    public int Id { get; private set; }
    public int SaleId { get; private set; }
    public int ProcessedByUserId { get; private set; }
    public DateTime CreatedAtUtc { get; private set; } = DateTime.UtcNow;

    public SaleReturnStatus Status { get; private set; } = SaleReturnStatus.Draft;
    public string Reason { get; private set; } = string.Empty;

    public IReadOnlyCollection<SaleReturnItem> Items => _items.AsReadOnly();

    public decimal TotalRefund => _items.Sum(x => x.LineTotal);

    private SaleReturn()
    {
    }

    public SaleReturn(int saleId, int userId, string reason)
    {
        if (saleId <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleId));

        if (userId <= 0)
            throw new ArgumentOutOfRangeException(nameof(userId));

        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException(
                "Return reason is required.", nameof(reason));

        SaleId = saleId;
        ProcessedByUserId = userId;
        Reason = reason.Trim();
    }

    public void AddItem(SaleReturnItem item)
    {
        if (Status != SaleReturnStatus.Draft)
            throw new InvalidOperationException(
                "Only draft returns can be edited.");

        ArgumentNullException.ThrowIfNull(item);

        if (_items.Any(x => x.SaleItemId == item.SaleItemId))
            throw new InvalidOperationException(
                "The same sale item cannot be added twice.");

        _items.Add(item);
    }

    public void Complete()
    {
        if (Status != SaleReturnStatus.Draft)
            throw new InvalidOperationException(
                "Only draft returns can be completed.");

        if (_items.Count == 0)
            throw new InvalidOperationException(
                "Return must contain items.");

        Status = SaleReturnStatus.Completed;
    }

    public void Cancel()
    {
        if (Status != SaleReturnStatus.Draft)
            throw new InvalidOperationException(
                "Only draft returns can be cancelled.");

        Status = SaleReturnStatus.Cancelled;
    }
}
