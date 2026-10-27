using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities
{
    public class Sale
    {
        private readonly List<SaleItem> _items = new();
        private readonly List<Payment> _payments = new();
        public int Id { get; private set; }
        // Nullable for draft/legacy sales; completed sales should be created through the sale service with context.
        public int? UserId { get; private set; }
        public int? CashShiftId { get; private set; }
        public int? TerminalId { get; private set; }
        public Guid? OperationId { get; private set; }
        public DateTime? CompletedAtUtc { get; private set; }
        public DateTime SaleDate { get; private set; }
        public decimal Subtotal => _items.Sum(item => item.TotalPrice);
        /// <summary>Invoice-level discount; there is no default discount.</summary>
        public decimal DiscountAmount { get; private set; }
        public int? DiscountAppliedByUserId { get; private set; }
        public string? CustomerPhone { get; private set; }
        /// <summary>Tax snapshot taken from settings when the sale is priced. Null means no tax applies.</summary>
        public string? TaxName { get; private set; }
        public decimal? TaxRate { get; private set; }
        public bool TaxIncluded { get; private set; } = true;
        public decimal TaxableAmount => Math.Max(0m, Subtotal - DiscountAmount);
        public decimal TaxAmount => TaxRate is null ? 0m : TaxIncluded ? decimal.Round(TaxableAmount - TaxableAmount / (1m + TaxRate.Value / 100m), 2, MidpointRounding.AwayFromZero) : decimal.Round(TaxableAmount * TaxRate.Value / 100m, 2, MidpointRounding.AwayFromZero);
        public decimal TotalAmount => TaxableAmount + (TaxRate is not null && !TaxIncluded ? TaxAmount : 0m);
        public IReadOnlyCollection<SaleItem> Items => _items.AsReadOnly();
        public IReadOnlyCollection<Payment> Payments => _payments.AsReadOnly();
        public decimal PaidAmount => _payments.Sum(payment => payment.Amount);
        public PaymentStatus PaymentStatus { get; private set; }
        public SaleReturnStatus Status { get; private set; }
        public Sale()
        {
            SaleDate = DateTime.Now;
            Status = SaleReturnStatus.Draft;
            PaymentStatus = PaymentStatus.Unpaid;
        }
        public void AssignContext(int userId, int cashShiftId, int terminalId, Guid operationId)
        {
            if (Status != SaleReturnStatus.Draft) throw new InvalidOperationException("Only draft sales can be assigned to a cashier context.");
            if (userId <= 0 || cashShiftId <= 0 || terminalId <= 0) throw new ArgumentOutOfRangeException("Context IDs must be positive.");
            if (operationId == Guid.Empty) throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
            UserId = userId; CashShiftId = cashShiftId; TerminalId = terminalId; OperationId = operationId;
        }

        public void ApplyDiscount(decimal amount, int appliedByUserId)
        {
            EnsureAdjustable();
            if (appliedByUserId <= 0) throw new ArgumentOutOfRangeException(nameof(appliedByUserId));
            if (amount < 0m || decimal.Round(amount, 2) != amount)
                throw new ArgumentOutOfRangeException(nameof(amount), "Discount must be zero or more with at most 2 decimals.");
            if (amount > Subtotal)
                throw new InvalidOperationException("Discount cannot exceed the invoice subtotal.");
            DiscountAmount = amount;
            DiscountAppliedByUserId = amount == 0m ? null : appliedByUserId;
        }

        public void ApplyTax(string name, decimal rate, bool included)
        {
            EnsureAdjustable();
            if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Tax name is required.", nameof(name));
            if (rate is < 0m or > 100m) throw new ArgumentOutOfRangeException(nameof(rate));
            TaxName = name.Trim(); TaxRate = rate; TaxIncluded = included;
        }

        public void ClearTax()
        {
            EnsureAdjustable();
            TaxName = null; TaxRate = null; TaxIncluded = true;
        }

        public void SetCustomerPhone(string? phone)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");
            if (string.IsNullOrWhiteSpace(phone)) { CustomerPhone = null; return; }
            string p = phone.Trim();
            if (p.Length is < 7 or > 20 || p.Any(c => !(char.IsDigit(c) || c is '+' or ' ' or '-')))
                throw new ArgumentException("Invalid phone number.", nameof(phone));
            CustomerPhone = p;
        }

        private void EnsureAdjustable()
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");
            if (_payments.Count > 0)
                throw new InvalidOperationException("Discount and tax cannot change after payment has been registered.");
        }

        /// <summary>For users allowed to sell without a cash shift (e.g. Admin). The sale is not tied to a drawer.</summary>
        public void AssignContextWithoutShift(int userId, Guid operationId)
        {
            if (Status != SaleReturnStatus.Draft) throw new InvalidOperationException("Only draft sales can be assigned to a cashier context.");
            if (userId <= 0) throw new ArgumentOutOfRangeException(nameof(userId));
            if (operationId == Guid.Empty) throw new ArgumentException("Operation ID cannot be empty.", nameof(operationId));
            UserId = userId; CashShiftId = null; TerminalId = null; OperationId = operationId;
        }

        public void AddItem(SaleItem item)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");

            if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (_items.Contains(item))
                throw new InvalidOperationException("This item has already been added to the sale.");

            _items.Add(item);
        }
        public void Complete()
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Only draft sales can be completed.");

            if (_items.Count == 0)
                throw new InvalidOperationException("Cannot complete a sale without items.");

            if (PaidAmount < TotalAmount)
                throw new InvalidOperationException("Cannot complete a sale before full payment.");

            Status = SaleReturnStatus.Completed;
            CompletedAtUtc = DateTime.UtcNow;
        }
        public void Cancel()
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Only draft sales can be cancelled.");

            Status = SaleReturnStatus.Cancelled;
        }
        public void ChangeItemQuantity(SaleItem item, decimal newQuantity)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");

            if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (!_items.Contains(item))
                throw new InvalidOperationException("This item does not belong to this sale.");

            item.ChangeQuantity(newQuantity);
        }

        public void AddProduct(Product product, decimal quantity = 1m)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");

            if (product is null)
                throw new ArgumentNullException(nameof(product));

            if (!product.IsActive)
                throw new InvalidOperationException("Cannot sell an inactive product.");

            if (quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero.");

            SaleItem? existingItem = _items.FirstOrDefault(item => item.ProductId == product.Id);

            if (existingItem is not null)
            {
                existingItem.ChangeQuantity(existingItem.Quantity + quantity);
                existingItem.ChangeUnitPrice(product.Price);
                return;
            }

            SaleItem newItem = new SaleItem(product.Id,product.Price,quantity);
            _items.Add(newItem);
        }
        public void AddPayment(Payment payment)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot add payment to a sale that is not in Draft status.");

            if (payment is null)
                throw new ArgumentNullException(nameof(payment));

            if (_items.Count == 0)
                throw new InvalidOperationException("Cannot add payment to a sale without items.");

            if (PaidAmount + payment.Amount > TotalAmount)
                throw new InvalidOperationException("Payment amount exceeds the remaining balance.");

            _payments.Add(payment);
            UpdatePaymentStatus();
        }
        private void UpdatePaymentStatus()
        {
            if (PaidAmount == 0)
                PaymentStatus = PaymentStatus.Unpaid;
            else if (PaidAmount < TotalAmount)
                PaymentStatus = PaymentStatus.PartiallyPaid;
            else
                PaymentStatus = PaymentStatus.Paid;
        }
        public void RemoveItem(SaleItem item)
        {
            if (Status != SaleReturnStatus.Draft)
                throw new InvalidOperationException("Cannot modify a sale that is not in Draft status.");

            if (item is null)
                throw new ArgumentNullException(nameof(item));

            if (!_items.Contains(item))
                throw new InvalidOperationException("This item does not belong to this sale.");

            if (_payments.Count > 0)
                throw new InvalidOperationException("Cannot remove items after payment has been registered.");

            _items.Remove(item);
        }
    }
}
