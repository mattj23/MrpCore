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
    
    public static string Description(this RouteOpAdd value)
    {
        return value switch {
            RouteOpAdd.Default => "Required",
            RouteOpAdd.NotDefault => "Optional",
            RouteOpAdd.Corrective => "Corrective",
            RouteOpAdd.Special => "Special",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, null)};
    }
    
}