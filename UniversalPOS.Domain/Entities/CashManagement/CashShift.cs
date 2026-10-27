using UniversalPOS.Domain.Enums;

namespace UniversalPOS.Domain.Entities;
public class CashShift 
{
    public int Id 
    {get; private set;} 
    public int UserId {get; private set;} 
    public int TerminalId {get; private set;} 
    public DateTime OpenedAtUtc {get; private set;} 
    public DateTime? ClosedAtUtc {get; private set;} 
    public decimal OpeningFloat {get; private set;} 
    public decimal? CountedCash {get; private set;} 
    public decimal? ExpectedCash {get; private set;} 
    public decimal? Difference {get; private set;} 
    public CashShiftStatus Status {get; private set;}=CashShiftStatus.Open; 
    public string? CloseNotes {get; private set;} public int? DifferenceApprovedByUserId {get; private set;} public DateTime? DifferenceApprovedAtUtc {get; private set;} private CashShift(){} 
    public CashShift(int userId,int terminalId,decimal openingFloat)
    {
        if(userId<=0||terminalId<=0)
            throw new ArgumentOutOfRangeException("User and terminal IDs must be positive.");
        if(openingFloat<0)
            throw new ArgumentOutOfRangeException(nameof(openingFloat));UserId=userId;TerminalId=terminalId;OpeningFloat=openingFloat;OpenedAtUtc=DateTime.UtcNow;Status=CashShiftStatus.Open;
    } public void RequestClose(decimal expectedCash,decimal countedCash,string? notes=null)
    {
        if(Status!=CashShiftStatus.Open)
            throw new InvalidOperationException("Only open shifts can be closed.");
        if(expectedCash<0||countedCash<0)
            throw new ArgumentOutOfRangeException("Cash values cannot be negative.");ExpectedCash=expectedCash;CountedCash=countedCash;Difference=countedCash-expectedCash;CloseNotes=notes?.Trim();ClosedAtUtc=DateTime.UtcNow;Status=Difference==0?CashShiftStatus.Closed:CashShiftStatus.PendingApproval;
    } public void ApproveDifference(int approverUserId)
    {
        if(Status!=CashShiftStatus.PendingApproval)
            throw new InvalidOperationException("No discrepancy is awaiting approval.");
        if(approverUserId<=0)
            throw new ArgumentOutOfRangeException(nameof(approverUserId));
        if(approverUserId==UserId)
            throw new InvalidOperationException("A cashier cannot approve the discrepancy of their own shift.");
        DifferenceApprovedByUserId=approverUserId;DifferenceApprovedAtUtc=DateTime.UtcNow;Status=CashShiftStatus.Closed;
    } 
}
