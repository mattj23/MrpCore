using MrpCore.Models;

namespace MrpCore;

public class MesManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> 
                            where TUnitState : UnitStateBase
                            where TProductType : ProductTypeBase
                            where TProductUnit : ProductUnitBase<TProductType>
                            where TRouteOperation : RouteOperationBase<TProductType>
                            where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>
                            where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult> _db;

    protected MesManager(MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> db)
    {
        _db = db;
    }

    public IQueryable<TProductType> ProductTypes()
    {
        return _db.Types;
    }


}