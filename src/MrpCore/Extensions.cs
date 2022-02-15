﻿using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore;

public static class Extensions
{
    public static string Description(this RouteOpFailure value)
    {
        return value switch {
            RouteOpFailure.Retry => "Permit Retry",
            RouteOpFailure.CorrectiveReturn => "Corrective Operations, then Retry",
            RouteOpFailure.CorrectiveProceed => "Corrective Operations, then Proceed",
            RouteOpFailure.TerminateWithSpecial => "Trigger Terminating Special",
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

    public static bool IsStandard(this RouteOpAdd value) => value is RouteOpAdd.Default or RouteOpAdd.NotDefault;
    public static bool NotSpecial(this RouteOpAdd value) => value is not RouteOpAdd.Special;

    public static string OpText<TProductType, TUnitState, TRouteOperation>(
        this Route<TProductType, TUnitState, TRouteOperation> route, int routeOpId)
        where TUnitState : UnitStateBase
        where TProductType : ProductTypeBase
        where TRouteOperation : RouteOperationBase<TProductType>
    {
        var op = route.AllById[routeOpId].Op;
        return route.OpText(op);
    }
    
    public static string OpText<TProductType, TUnitState, TRouteOperation>(
        this Route<TProductType, TUnitState, TRouteOperation> route, TRouteOperation op)
        where TUnitState : UnitStateBase
        where TProductType : ProductTypeBase
        where TRouteOperation : RouteOperationBase<TProductType>
    {
        if (op.AddBehavior.IsStandard()) return $"OP {op.OpNumber}";
        if (op.AddBehavior == RouteOpAdd.Special) return "-";
        if (op.AddBehavior is RouteOpAdd.Corrective)
        {
            var prefix = route.OpText(route.ActiveById[op.CorrectiveId].Op);
            return $"{prefix}.C{op.OpNumber}";
        }

        throw new NotImplementedException();
    }
}