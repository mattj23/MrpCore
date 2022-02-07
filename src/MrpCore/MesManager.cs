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

    public ValueTask<TProductType?> ProductTypeById(int id)
    {
        return _db.Types.FindAsync(id);
    }
    
    public async Task<int> CreateProductType(TProductType newItem)
    {
        await _db.Types.AddAsync(newItem);
        await _db.SaveChangesAsync();
        return newItem.Id;
    }
    public IQueryable<TUnitState> UnitStates()
    {
        return _db.States;
    }

    public ValueTask<TUnitState?> UnitStateById(int id)
    {
        return _db.States.FindAsync(id);
    }

    public async Task<int> CreateUnitState(TUnitState newItem)
    {
        await _db.States.AddAsync(newItem);
        await _db.SaveChangesAsync();
        return newItem.Id;
    }

}