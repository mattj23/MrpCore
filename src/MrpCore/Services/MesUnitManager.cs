using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore.Services;

public class MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>, new()
    where TOperationResult :
    OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>, new()
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult> _db;

    private readonly MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult> _routes;

    private readonly IMesUpdater _updater;
    
    public MesUnitManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> db, 
        MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> routes, IMesUpdater updater)
    {
        _db = db;
        _routes = routes;
        _updater = updater;
    }

    public IQueryable<TProductUnit> Units => _db.Units.AsNoTracking();

    public ValueTask<TProductUnit?> GetUnit(int unitId)
    {
        return _db.Units.FindAsync(unitId);
    }
    
    /// <summary>
    /// Adds a new product unit and creates its unit route from the product type's master route
    /// </summary>
    /// <param name="newUnit"></param>
    /// <param name="modifyOperations"></param>
    public async Task AddUnit(TProductUnit newUnit, Action<TUnitOperation[]>? modifyOperations = null)
    {
        newUnit.Id = 0;
        if (newUnit.CreatedUtc == default) newUnit.CreatedUtc = DateTime.UtcNow;
        
        await _db.Units.AddAsync(newUnit);
        await _db.SaveChangesAsync();
        
        var route = await _routes.GetRoute(newUnit.ProductTypeId);
        var operations = route.DefaultOperations.Select(o => new TUnitOperation
        {
            ProductUnitId = newUnit.Id,
            RouteOperationId = o.Id
        }).ToArray();

        modifyOperations?.Invoke(operations);
        await _db.UnitOperations.AddRangeAsync(operations);
        await _db.SaveChangesAsync();
        _updater.UpdateUnit(ChangeType.Created, newUnit.Id);
    }
    
    public async
    Task<UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>>
    GetUnitRoute(int unitId)
    {
        var unitOps = await _db.UnitOperations.AsNoTracking()
            .Where(o => o.ProductUnitId == unitId)
            .Include(o => o.RouteOperation)
            .ToArrayAsync();

        if (!unitOps.Any()) throw new KeyNotFoundException("No operations found for this unit");

        var routeOps = unitOps.Select(o => o.RouteOperationId).ToHashSet();
        var changes = new Dictionary<int, StateRelations<TUnitState>>();
        foreach (var opId in routeOps)
            changes.Add(opId, await _routes.GetStates(opId));
        
        var opIds = unitOps.Select(o => o.Id).ToHashSet();
        var results = await _db.OperationResults.AsNoTracking().Where(r => opIds.Contains(r.UnitOperationId))
            .ToArrayAsync();

        return new UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>(
            unitId, results, unitOps, changes);
    }

    public async Task AddOpToRoute(int unitId, int routeOpId, Action<TUnitOperation>? modifyOperation=null)
    {
        var unit = await _db.Units.FindAsync(unitId);
        if (unit is null) throw new KeyNotFoundException("Could not find the unit specified");

        var routeOperation = await _db.RouteOperations.FindAsync(routeOpId);
        if (routeOperation is null) throw new KeyNotFoundException("Could not find the route operation ID");

        // Create or update the associated unit operation
        var operation = await _db.UnitOperations
            .FirstOrDefaultAsync(o => o.ProductUnitId == unitId && o.RouteOperationId == routeOpId);
        if (operation is null)
        {
            operation = new TUnitOperation {ProductUnitId = unitId, RouteOperationId = routeOpId};
            await _db.UnitOperations.AddAsync(operation);
        }
        
        modifyOperation?.Invoke(operation);
        await _db.SaveChangesAsync();
        _updater.UpdateUnit(ChangeType.Updated, unitId);
    }
        
    public async Task ApplySpecialOp(int unitId, int routeOpId, TOperationResult result,
        Action<TUnitOperation>? modifyOperation = null)
    {
        var unit = await _db.Units.FindAsync(unitId);
        if (unit is null) throw new KeyNotFoundException("Could not find the unit specified");

        var routeOperation = await _db.RouteOperations.FindAsync(routeOpId);
        if (routeOperation is null) throw new KeyNotFoundException("Could not find the special route operation ID");
        if (routeOperation.AddBehavior is not RouteOpAdd.Special)
            throw new ArgumentException("The specified operation is not special");

        // Create or update the associated unit operation
        var operation = await _db.UnitOperations
            .FirstOrDefaultAsync(o => o.ProductUnitId == unitId && o.RouteOperationId == routeOpId);
        if (operation is null)
        {
            operation = new TUnitOperation {ProductUnitId = unitId, RouteOperationId = routeOpId};
            await _db.UnitOperations.AddAsync(operation);
        }
        
        modifyOperation?.Invoke(operation);
        await _db.SaveChangesAsync();

        // Add the result to the unit operation
        result.Id = 0;
        result.UnitOperationId = operation.Id;
        if (result.UtcTime == default) result.UtcTime = DateTime.UtcNow;
        result.Pass = true;
        await _db.OperationResults.AddAsync(result);
        await _db.SaveChangesAsync();
        _updater.UpdateUnit(ChangeType.Updated, unitId);
    }

    public async Task ApplyResult(int unitId, int opId, TOperationResult result,
        Action<TUnitOperation[]>? modifyCorrective=null, 
        Action<TUnitOperation>? modifySpecial = null)
    {
        var unitRoute = await GetUnitRoute(unitId);

        if (unitRoute.NextOperation?.Id != opId)
            throw new ArgumentException("The unit operation ID specified is not the next operation which needs to " +
                                        "run on this unit");

        // Prepare and post the result
        result.Id = 0;
        result.UnitOperationId = opId;
        if (result.UtcTime == default) 
            result.UtcTime = DateTime.UtcNow;

        await _db.OperationResults.AddAsync(result);
        await _db.SaveChangesAsync();
        
        // Exit if the operation passed
        if (result.Pass)
        {
            _updater.UpdateResult(true, result.Id);
            _updater.UpdateUnit(ChangeType.Updated, unitId);
        };

        // If the operation failed we may need to perform additional actions based on the route operation
        var routeOperation = await _db.RouteOperations.FindAsync(unitRoute.NextOperation.RouteOperationId);
        if (routeOperation is null) throw new KeyNotFoundException("Route operation not found");

        // Find and apply any special operations
        if (routeOperation.SpecialFailId is not null && routeOperation.SpecialFailId > 0)
        {
            var special = await _routes.GetByRootId((int)routeOperation.SpecialFailId);
            if (special is null)
                throw new KeyNotFoundException($"Could not find route by root ID {routeOperation.SpecialFailId}");

            await ApplySpecialOp(unitId, special.Id, new TOperationResult { }, modifySpecial);
        }

        // Check if the route operation had a corrective path and create it if necessary
        if (routeOperation.FailureBehavior is RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn)
        {
            var correctiveOperations = await _db.RouteOperations
                .Where(o => o.AddBehavior == RouteOpAdd.Corrective && o.CorrectiveId == routeOperation.RootId)
                .ToArrayAsync();
            var missing = correctiveOperations.Where(o => !unitRoute.HasRouteOp(o.Id))
                .Select(o => new TUnitOperation { ProductUnitId = unitId, RouteOperationId = o.Id })
                .ToArray();
            modifyCorrective?.Invoke(missing);
            await _db.UnitOperations.AddRangeAsync(missing);
        }

        await _db.SaveChangesAsync();
        
        _updater.UpdateResult(false, result.Id);
        _updater.UpdateUnit(ChangeType.Updated, unitId);
    } 
}