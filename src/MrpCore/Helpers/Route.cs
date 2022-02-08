using MrpCore.Models;

namespace MrpCore.Helpers;

public class Route<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    private readonly RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] _allOperations;
    private readonly RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] _special;
    private readonly RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] _standard;

    public Route(RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] allOperations)
    {
        _allOperations = allOperations;
        _special = _allOperations.Where(o => o.Operation.AddBehavior is RouteOpAdd.Special).ToArray();
        _standard = _allOperations.Where(o => o.Operation.AddBehavior is RouteOpAdd.NotDefault or RouteOpAdd.Default)
            .OrderBy(o => o.Operation.OpNumber)
            .ToArray();
    }

    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> Special => _special;
    
    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> Standard => _standard;
    
    
}