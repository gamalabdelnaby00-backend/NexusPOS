using System;
using System.Collections.Generic;
using System.Text;

namespace UniversalPOS.Application.DTOs.Sales;

public class SaleItemDto
{
    public int ProductId { get; init; }
    public string ProductName { get; init; } = string.Empty;
    public decimal UnitPrice { get; init; }
    public decimal Quantity { get; init; }
    public decimal TotalPrice { get; init; }
}

