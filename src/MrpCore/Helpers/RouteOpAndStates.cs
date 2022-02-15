using MrpCore.Models;

namespace MrpCore.Helpers;

public class RouteOpAndStates<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    public RouteOpAndStates(TRouteOperation operation, StateRelations<TUnitState> states)
    {
        Op = operation;
        States = states;
    }

    public TRouteOperation Op { get; }
    public StateRelations<TUnitState> States { get; }
}