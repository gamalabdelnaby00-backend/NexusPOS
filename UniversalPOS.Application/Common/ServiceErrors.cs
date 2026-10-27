namespace UniversalPOS.Application.Common;

/// <summary>
/// Stable error codes carried in <see cref="Exception.Data"/> so the UI can show a localized,
/// non-technical message without parsing English exception text.
/// </summary>
public static class ServiceErrors
{
    public const string CodeKey = "ErrorCode";

    public const string ShiftRequired = "SHIFT_REQUIRED";
    public const string ShiftNotOwned = "SHIFT_NOT_OWNED";
    public const string ShiftNotOpen = "SHIFT_NOT_OPEN";
    public const string InsufficientStock = "INSUFFICIENT_STOCK";
    public const string PermissionDenied = "PERMISSION_DENIED";

    public static InvalidOperationException Create(string code, string message)
    {
        var ex = new InvalidOperationException(message);
        ex.Data[CodeKey] = code;
        return ex;
    }

    public static string? GetCode(Exception ex) =>
        ex.Data.Contains(CodeKey) ? ex.Data[CodeKey] as string : null;
}

/// <summary>Thrown by repositories when a database unique constraint rejects a value (e.g. two users saving the same code at once).</summary>
public sealed class DuplicateValueException : Exception
{
    public string Field { get; }

    public DuplicateValueException(string field) : base($"Duplicate value for {field}.")
    {
        Field = field;
    }
}
