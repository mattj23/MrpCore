using MrpCore.Models;

namespace MrpCore.Helpers;

public class RouteOpAndStates<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    public RouteOpAndStates(TRouteOperation operation, OpStateChanges<TUnitState> states)
    {
        Op = operation;
        States = states;
    }

    public TRouteOperation Op { get; }
    public OpStateChanges<TUnitState> States { get; }
}