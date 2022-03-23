using MrpCore.Models;

namespace MrpCore.Helpers;

/// <summary>
/// Checks a route for problems
/// </summary>
/// <remarks>
/// Routes, referring to the "master route" defined by a product type, have properties that cannot be enforced directly
/// through the data model.  This object checks for the following conditions:
/// 
/// 1. No operations in the same context (standard or corrective) have the same op number
/// 2. Any operation which terminates on a special op on failure references a special op that applies a terminal state
/// 3. Operations which reference corrective action chains actually have valid corrective operations
/// 
/// </remarks>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TToolRequirement"></typeparam>
public class RouteValidator<TProductType, TUnitState, TRouteOperation, TToolRequirement>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
    where TToolRequirement : ToolRequirementBase
{
    private readonly Route<TProductType, TUnitState, TRouteOperation, TToolRequirement> _route;

    public RouteValidator(Route<TProductType, TUnitState, TRouteOperation, TToolRequirement> route)
    {
        _route = route;
    }
}
