using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore;

public class MesManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> 
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>, new()
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

    public async Task<Route<TProductType, TUnitState, TRouteOperation>> GetRoute(int productTypeId)
    {
        var routeOps = await _db.RouteOperations.AsNoTracking()
            .Where(r => r.ProductTypeId == productTypeId)
            .ToListAsync();

        var routeOpIds = routeOps.Select(r => r.Id).ToHashSet();

        var joins = await _db.StatesToRoutes.AsNoTracking()
            .Where(s => routeOpIds.Contains(s.RouteOperationId))
            .ToListAsync();

        var referencedStateIds = joins.Select(j => j.UnitStateId).ToHashSet();

        var states = await _db.States.AsNoTracking()
            .Where(s => referencedStateIds.Contains(s.Id))
            .ToDictionaryAsync(s => s.Id, s => s);

        var results = new List<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>>();
        foreach (var op in routeOps)
        {
            var adds = new List<TUnitState>();
            var removes = new List<TUnitState>();
            var opJoins = joins.Where(j => j.RouteOperationId == op.Id);
            foreach (var opJoin in opJoins)
            {
                if (opJoin.IsAdd)
                    adds.Add(states[opJoin.UnitStateId]);
                else 
                    removes.Add(states[opJoin.UnitStateId]);
            }

            results.Add(new RouteOpAndStates<TProductType, TUnitState, TRouteOperation>(op, adds.ToArray(), removes.ToArray()));
        }

        return new Route<TProductType, TUnitState, TRouteOperation>(productTypeId, results.ToArray());
    }

    public async Task<int> AddRouteOperation(TRouteOperation operation, TUnitState[] addedStates,
        TUnitState[] removedStates)
    {
        await _db.RouteOperations.AddAsync(operation);
        await _db.SaveChangesAsync();
        
        var addJoins = addedStates.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = true, RouteOperationId = operation.Id, UnitStateId = s.Id
        });
        var removeJoins = removedStates.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = false, RouteOperationId = operation.Id, UnitStateId = s.Id
        });
        await _db.StatesToRoutes.AddRangeAsync(addJoins.Concat(removeJoins));
        await _db.SaveChangesAsync();

        return operation.Id;
    }
    
    public async Task UpdateRouteOperation(int id, Action<TRouteOperation> modifyAction, TUnitState[] addedStates,
        TUnitState[] removedStates)
    {
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        
        modifyAction.Invoke(item);
        await _db.SaveChangesAsync();
        
        var joins = _db.StatesToRoutes.Where(j => j.RouteOperationId == id).ToArray();

        var newJoins = addedStates.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = true, RouteOperationId = item.Id, UnitStateId = s.Id
        }).Concat(removedStates.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = false, RouteOperationId = item.Id, UnitStateId = s.Id
        })).ToArray();
        
        // Any joins that don't exist that need to exist
        var toAdd = newJoins.Where(nj => !joins.Any(j => j.UnitStateId == nj.UnitStateId && j.IsAdd == nj.IsAdd))
            .ToArray();
        var toRemove = joins.Where(j => !newJoins.Any(nj => j.UnitStateId == nj.UnitStateId && j.IsAdd == nj.IsAdd))
                                   .ToArray();
        
        await _db.StatesToRoutes.AddRangeAsync(toAdd);
        _db.StatesToRoutes.RemoveRange(toRemove);
        await _db.SaveChangesAsync();
    }
    
    public async Task DeleteRouteOperation(int id)
    {
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        
        var joins = _db.StatesToRoutes.Where(j => j.RouteOperationId == id).ToArray();
        
        _db.StatesToRoutes.RemoveRange(joins);
        _db.RouteOperations.Remove(item);
        await _db.SaveChangesAsync();
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
        
        var route = await this.GetRoute(newUnit.ProductTypeId);
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