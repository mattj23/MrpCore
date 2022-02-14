using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore;

public class MesManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> 
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>, new()
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>, new()
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult> _db;

    protected MesManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> db)
    {
        _db = db;
        RouteManager =
            new MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult>(_db);
        
        UnitManager =
            new MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult>(_db, RouteManager);

    }

    public MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        RouteManager { get; }
    
    public MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        UnitManager { get; }
    
    
    public IQueryable<TProductType> ProductTypes => _db.Types;
    public ValueTask<TProductType?> ProductTypeById(int id) => _db.Types.FindAsync(id);
    public IQueryable<TUnitState> UnitStates => _db.States;

    public ValueTask<TUnitState?> UnitStateById(int id) => _db.States.FindAsync(id); 
    
    public async Task<int> CreateProductType(TProductType newItem)
    {
        await _db.Types.AddAsync(newItem);
        await _db.SaveChangesAsync();
        return newItem.Id;
    }

    public async Task<int> CreateUnitState(TUnitState newItem)
    {
        await _db.States.AddAsync(newItem);
        await _db.SaveChangesAsync();
        return newItem.Id;
    }

}