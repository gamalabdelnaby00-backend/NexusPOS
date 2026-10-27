using System;
using System.Collections.Generic;
using System.Text;

namespace UniversalPOS.Domain.Enums
{
    public enum CashMovementType
    {
        CashSale = 0, 
        CashRefund = 1, 
        OpeningFloat = 2, 
        CashIn = 3, 
        CashOut = 4, 
        Expense = 5, 
        SafeDrop = 6, 
        Correction = 7
    }
}
