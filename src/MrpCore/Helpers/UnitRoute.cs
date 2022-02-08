using MrpCore.Models;

namespace MrpCore.Helpers;

public class UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>, new()
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    private readonly int _unitId;
    private readonly TOperationResult[] _results;
    private readonly TUnitOperation[] _operations;

    public UnitRoute(int unitId, TOperationResult[] results, TUnitOperation[] operations)
    {
        _unitId = unitId;
        _results = results;
        _operations = operations;
    }
    
    
}