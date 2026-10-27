
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities
{
    public class Product
    {
        public int Id { get; private set; }

        public string Name { get; private set; } = string.Empty;

        public decimal Price { get; private set; }

        public string Barcode { get; private set; } = string.Empty;

        /// <summary>Object-storage key, not a local path or a public URL.</summary>
        public string? ImageKey { get; private set; }

        public int CategoryId { get; private set; }

        public bool IsActive { get; private set; }

        public UnitOfMeasure UnitOfMeasure { get; private set; } = UnitOfMeasure.Piece;
        /// <summary>Business code shown to users (search, reports). Unique. Defaults to the barcode.</summary>
        public string Code { get; private set; } = string.Empty;
        /// <summary>Purchase cost per unit; used for inventory valuation, never the selling price.</summary>
        public decimal CostPrice { get; private set; }
        /// <summary>Reorder level; stock at or below it is flagged for re-supply.</summary>
        public decimal MinStock { get; private set; }

        public Product(string name, decimal price, string barcode, int categoryId, UnitOfMeasure unitOfMeasure = UnitOfMeasure.Piece, string? code = null, decimal costPrice = 0m, decimal minStock = 0m)
        {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Product name cannot be empty.");

            if (price < 0)
                throw new ArgumentException("Price cannot be negative.");

            if (string.IsNullOrWhiteSpace(barcode))
                throw new ArgumentException("Barcode cannot be empty.");

            if (categoryId <= 0)
                throw new ArgumentException("Invalid category ID.");

            if (costPrice < 0)
                throw new ArgumentException("Cost price cannot be negative.");
            if (minStock < 0)
                throw new ArgumentException("Minimum stock cannot be negative.");
            Code = string.IsNullOrWhiteSpace(code) ? barcode.Trim() : code.Trim();
            CostPrice = costPrice;
            MinStock = minStock;
            Name = name;
            Price = price;
            Barcode = barcode;
            CategoryId = categoryId;
            UnitOfMeasure = unitOfMeasure;
            IsActive = true;
        }

        public void SetImageKey(string? imageKey)
        {
            if (imageKey is not null && (string.IsNullOrWhiteSpace(imageKey) || imageKey.StartsWith("/", StringComparison.Ordinal) || imageKey.Contains("..", StringComparison.Ordinal)))
                throw new ArgumentException("Image key must be a relative object-storage key.", nameof(imageKey));

            ImageKey = string.IsNullOrWhiteSpace(imageKey) ? null : imageKey.Trim();
        }

        public void ChangePrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException("Price cannot be negative.");

            Price = newPrice;
        }

        public void ChangeCost(decimal newCost)
        {
            if (newCost < 0)
                throw new ArgumentException("Cost price cannot be negative.");
            CostPrice = newCost;
        }

        public void ChangeMinStock(decimal minStock)
        {
            if (minStock < 0)
                throw new ArgumentException("Minimum stock cannot be negative.");
            MinStock = minStock;
        }

        public void ChangeCode(string code)
        {
            if (string.IsNullOrWhiteSpace(code))
                throw new ArgumentException("Product code cannot be empty.");
            Code = code.Trim();
        }

        public void Deactivate()
        {
            IsActive = false;
        }

        public void Activate()
        {
            IsActive = true;
        }
    }
}
