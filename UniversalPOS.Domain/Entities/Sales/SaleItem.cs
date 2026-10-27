using System;
using System.Collections.Generic;
using System.Text;

namespace UniversalPOS.Domain.Entities
{
    public class SaleItem
    {
        public int Id { get; private set; }

        public int ProductId { get; private set; }

        public decimal UnitPrice { get; private set; }

        public decimal Quantity { get; private set; }

        public decimal TotalPrice => UnitPrice * Quantity;
        public SaleItem(int productId, decimal unitPrice, decimal quantity)
        {
            if (productId <= 0)
                throw new ArgumentOutOfRangeException(nameof(productId), "Invalid product ID.");

            ProductId = productId;

            if (unitPrice < 0)
                throw new ArgumentOutOfRangeException(nameof(unitPrice), "Unit price cannot be negative.");

            UnitPrice = unitPrice;

            if (quantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be greater than zero.");

            Quantity = quantity;
        }

        internal void ChangeQuantity(decimal newQuantity)
        {
            if (newQuantity <= 0)
                throw new ArgumentOutOfRangeException(nameof(newQuantity), "Quantity must be greater than zero.");

            Quantity = newQuantity;
        }

        public void ChangeUnitPrice(decimal newPrice)
        {
            if (newPrice < 0)
                throw new ArgumentException(nameof(newPrice), "Unit price cannot be negative.");

            UnitPrice = newPrice;
        }
    }
}

