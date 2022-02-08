using MrpCore.Models;

namespace MrpCore;

public static class Extensions
{
    public static string Description(this RouteOpOnFailure value)
    {
        return value switch {
            RouteOpOnFailure.Retry => "Permit Retry",
            RouteOpOnFailure.Corrective => "Corrective Operations",
            RouteOpOnFailure.Special => "Trigger Special",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)};
    }
    
}