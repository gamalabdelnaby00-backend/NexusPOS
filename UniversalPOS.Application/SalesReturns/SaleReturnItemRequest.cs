
namespace UniversalPOS.Application.SalesReturns;

public sealed record SaleReturnItemRequest(int SaleItemId,decimal Quantity,bool IsRestockable);
