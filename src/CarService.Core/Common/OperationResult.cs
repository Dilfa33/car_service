namespace CarService.Core.Common;

// WHAT: A tiny "it worked / it didn't, and here's why" return value.
// WHY:  Expected failures (a rule was broken) are not exceptions. Exceptions are for the
//       unexpected (DB down). Returning a result keeps the flow readable and lets the
//       controller show the message to the user.
public record OperationResult(bool Succeeded, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}
