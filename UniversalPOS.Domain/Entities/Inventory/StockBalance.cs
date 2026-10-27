
namespace UniversalPOS.Domain.Entities;

public class StockBalance
{
    public int ProductId { get; private set; }

    public decimal Quantity { get; private set; }

    public byte[] RowVersion { get; private set; } = Array.Empty<byte>();

    private StockBalance()
    {
    }

    public StockBalance(int productId, decimal openingQuantity = 0m)
    {
        if (productId <= 0)
            throw new ArgumentOutOfRangeException(nameof(productId));

        if (openingQuantity < 0m)
            throw new ArgumentOutOfRangeException(nameof(openingQuantity),"Opening quantity cannot be negative.");
        if (decimal.Round(openingQuantity, 3) != openingQuantity)
            throw new ArgumentOutOfRangeException(nameof(openingQuantity),"Quantity cannot have more than 3 decimal places.");

        ProductId = productId;
        Quantity = openingQuantity;
    }

    public void Increase(decimal quantity)
    {
        if (quantity <= 0)
            throw new ArgumentOutOfRangeException(nameof(quantity));

        if (decimal.Round(quantity, 3) != quantity)
            throw new ArgumentOutOfRangeException(nameof(quantity),"Quantity cannot have more than 3 decimal places.");
        Quantity += quantity;
    }

    public void Decrease(decimal quantity)
    {
        ValidateQuantity(quantity);

        if (quantity > Quantity)
        {
            throw new InvalidOperationException("Insufficient stock balance.");
        }

        Quantity -= quantity;
    }

    private static void ValidateQuantity(decimal quantity)
    {
        if (quantity <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity),"Quantity must be greater than zero.");
        }

        if (decimal.Round(quantity, 3) != quantity)
        {
            throw new ArgumentOutOfRangeException(nameof(quantity),"Quantity cannot have more than 3 decimal places.");
        }
    }
}
