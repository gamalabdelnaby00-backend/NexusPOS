using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities;
public class CashMovement 
{ 
    public int Id {get; private set;} 
    public int CashShiftId {get; private set;} 
    public CashMovementType Type {get; private set;} 
    public decimal Amount {get; private set;} 
    public bool IncreasesDrawer {get; private set;} 
    public string Reason {get; private set;}=string.Empty; 
    public DateTime CreatedAtUtc {get; private set;}=DateTime.UtcNow; 
    public int CreatedByUserId {get; private set;} 
    public int? ReversesMovementId {get; private set;} 
    private CashMovement()
    {} 
    public CashMovement(int shiftId,CashMovementType type,decimal amount,bool increasesDrawer,string reason,int createdByUserId,int? reversesMovementId=null)
    {
        if(shiftId<=0||createdByUserId<=0)
            throw new ArgumentOutOfRangeException("IDs must be positive.");
        if(amount<=0)
            throw new ArgumentOutOfRangeException(nameof(amount));
        if(string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Reason is required.");

        CashShiftId = shiftId;
        Type = type;
        Amount = amount;
        IncreasesDrawer = increasesDrawer;
        Reason = reason.Trim();
        CreatedByUserId = createdByUserId;
        ReversesMovementId = reversesMovementId;
    } 
}
