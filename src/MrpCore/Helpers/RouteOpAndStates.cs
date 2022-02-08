using MrpCore.Models;

namespace MrpCore.Helpers;

public class RouteOpAndStates<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    public RouteOpAndStates(TRouteOperation operation, TUnitState[] adds, TUnitState[] removes)
    {
        Operation = operation;
        Adds = adds;
        Removes = removes;
    }

    public TRouteOperation Operation { get; }
    public TUnitState[] Adds { get; }
    public TUnitState[] Removes { get; set; }

}