using System;
using System.Collections.Generic;
using System.Text;
using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Application.DTOs.Sales;

public class SaleDetailsDto
{
    public int Id { get; init; }
    public DateTime SaleDate { get; init; }
    public SaleReturnStatus Status { get; init; }
    public PaymentStatus PaymentStatus { get; init; }

    public decimal TotalAmount { get; init; }
    public decimal PaidAmount { get; init; }
    public decimal RemainingAmount => TotalAmount - PaidAmount;
    public List<SaleItemDto> Items { get; init; } = new();
}

