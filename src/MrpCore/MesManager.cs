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
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
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
    }

    public MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        RouteManager { get; }
    
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

    /// <summary>
    /// Adds a new product unit and creates its unit route from the product type's master route
    /// </summary>
    /// <param name="newUnit"></param>
    /// <param name="modifyOperations"></param>
    public async Task AddUnit(TProductUnit newUnit, Action<TUnitOperation[]>? modifyOperations)
    {
        await _db.Units.AddAsync(newUnit);
        await _db.SaveChangesAsync();
        
        var route = await this.RouteManager.GetRoute(newUnit.ProductTypeId);
        var operations = route.DefaultOperations.Select(o => new TUnitOperation
        {
            ProductUnitId = newUnit.Id,
            RouteOperationId = o.Id
        }).ToArray();

        modifyOperations?.Invoke(operations);
        await _db.UnitOperations.AddRangeAsync(operations);
        await _db.SaveChangesAsync();
    }

    public async
    Task<UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>>
    GetUnitRoute(int unitId)
    {
        var unitOps = await _db.UnitOperations.AsNoTracking()
            .Where(o => o.ProductUnitId == unitId)
            .Include(o => o.RouteOperation)
            .ToArrayAsync();
        
        var opIds = unitOps.Select(o => o.Id).ToHashSet();
        var results = await _db.OperationResults.AsNoTracking().Where(r => opIds.Contains(r.UnitOperationId))
            .ToArrayAsync();

        return new UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>(
            unitId, results, unitOps);
    }
}