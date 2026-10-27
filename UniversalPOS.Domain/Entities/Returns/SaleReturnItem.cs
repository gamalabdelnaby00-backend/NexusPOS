
using System;

namespace UniversalPOS.Domain.Entities;

public class SaleReturnItem
{
    public int Id { get; private set; }
    public int SaleItemId { get; private set; }
    public int ProductId { get; private set; }

    public decimal Quantity { get; private set; }
    public decimal UnitRefund { get; private set; }
    public bool IsRestockable { get; private set; }

    public decimal LineTotal => Quantity * UnitRefund;

    private SaleReturnItem()
    {
    }

    public SaleReturnItem(
        int saleItemId,
        int productId,
        decimal quantity,
        decimal unitRefund,
        bool isRestockable)
    {
        if (saleItemId <= 0)
            throw new ArgumentOutOfRangeException(nameof(saleItemId));

        if (productId <= 0)
            throw new ArgumentOutOfRangeException(nameof(productId));

        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (decimal.Round(quantity, 3) != quantity)
            throw new ArgumentOutOfRangeException(
                nameof(quantity),
                "Quantity cannot have more than 3 decimal places.");

        if (unitRefund < 0)
            throw new ArgumentOutOfRangeException(nameof(unitRefund));

        if (decimal.Round(unitRefund, 2) != unitRefund)
            throw new ArgumentOutOfRangeException(
                nameof(unitRefund),
                "Refund price cannot have more than 2 decimal places.");

        SaleItemId = saleItemId;
        ProductId = productId;
        Quantity = quantity;
        UnitRefund = unitRefund;
        IsRestockable = isRestockable;
    }
}
