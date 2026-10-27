using System;
using System.Collections.Generic;
using System.Text;

namespace UniversalPOS.Domain.Enums;

public enum StockMovementType
{
    PurchaseReceipt = 0,
    Sale = 1,
    CustomerReturn = 2,
    SupplierReturn = 3,
    AdjustmentIncrease = 4,
    AdjustmentDecrease = 5,
    Reversal = 6
}