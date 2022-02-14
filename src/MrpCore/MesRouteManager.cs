using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore;

/// <summary>
/// Manager class which specifically handles operations related to Route Operations/master routes.
/// </summary>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TProductUnit"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TUnitOperation"></typeparam>
/// <typeparam name="TOperationResult"></typeparam>
public class MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>, new()
    where TOperationResult :
    OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult> _db;

    public MesRouteManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> db)
    {
        _db = db;
    }

    /// <summary>
    /// Queries to determine if any TUnitOperation references the Route Operation with the specified ID. If the
    /// operation has been referenced it is no longer editable.
    /// </summary>
    /// <param name="routeOperationId">The ID of the operation being checked</param>
    /// <returns>true if the route operation cannot be edited, false if it is safe to edit</returns>
    public Task<bool> IsOpLocked(int routeOperationId)
    {
        return _db.UnitOperations.AnyAsync(o => o.RouteOperationId == routeOperationId);
    }

    /// <summary>
    /// Get the highest version of a route operation by its root ID, regardless of whether it is active/archived.
    /// </summary>
    /// <param name="rootId"></param>
    /// <returns></returns>
    public async Task<TRouteOperation?> GetByRootId(int rootId)
    {
        return (await _db.RouteOperations.Where(o => o.RootId == rootId).ToArrayAsync())
            .MaxBy(o => o.RootVersion);
    }
    
    /// <summary>
    /// Add a new Route Operation to the data store. This will automatically set the RootId and RootVersion. Do not
    /// use this method to add an already existing operation.
    /// </summary>
    /// <param name="operation">A new TRouteOperation to be added to the data store</param>
    /// <param name="stateChanges"></param>
    /// <returns>The integer ID of the new added operation</returns>
    public async Task<int> AddOp(TRouteOperation operation, OpStateChanges<TUnitState> stateChanges)
    {
        operation.Id = 0;
        operation.ThrowIfInvalid();
        
        await _db.RouteOperations.AddAsync(operation);
        await _db.SaveChangesAsync();

        operation.RootId = operation.Id;
        operation.RootVersion = 0;

        var joins = GetJoins(operation.Id, stateChanges);
        await _db.StatesToRoutes.AddRangeAsync(joins);
        await _db.SaveChangesAsync();

        return operation.Id;
    }

    /// <summary>
    /// Adds a new corrective operation to a parent route operation with corrective failure behavior. Handles the
    /// setting of the internal `CorrectiveId` and `AddBehavior`.
    /// </summary>
    /// <param name="operation"></param>
    /// <param name="parentId"></param>
    /// <param name="states"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public async Task<int> AddCorrectiveOp(TRouteOperation operation, int parentId, OpStateChanges<TUnitState> states)
    {
        // Check for a valid parent
        var parent = await _db.RouteOperations.FindAsync(parentId);
        if (parent is null) throw new KeyNotFoundException();
        if (parent.FailureBehavior is not (RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn))
        {
            throw new InvalidOperationException(
                "A corrective operation must be added to a parent with corrective failure behavior");
        }
        
        operation.CorrectiveId = parent.RootId;
        operation.AddBehavior = RouteOpAdd.Corrective;

        return await AddOp(operation, states);
    }

    /// <summary>
    /// Modifies a Route Operation by fetching it from the data store and invoking an Action on it. The operation will
    /// throw an exception if the Route Operation is already referenced. Use IsOpLocked(id) to determine ahead of time
    /// if an operation is safe to modify. The addedStates and removedStates arguments will replace the existing
    /// relations for the Route Operation.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to edit</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="stateChanges"></param>
    /// <exception cref="InvalidOperationException">Thrown if the Route Operation is already referenced by a product unit</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the operation ID cannot be found</exception>
    public async Task UpdateOp(int id, Action<TRouteOperation> modifyAction, OpStateChanges<TUnitState> stateChanges)
    {
        if (await IsOpLocked(id))
            throw new InvalidOperationException(
                "This route operation has already been referenced and cannot be edited.");
        
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        
        modifyAction.Invoke(item);
        item.ThrowIfInvalid();
        await _db.SaveChangesAsync();
        
        var joins = _db.StatesToRoutes.Where(j => j.RouteOperationId == id).ToArray();
        var newJoins = GetJoins(id, stateChanges);
        
        // Any joins that don't exist that need to exist
        var toAdd = newJoins.Where(nj => !joins.Any(j => j.UnitStateId == nj.UnitStateId && j.IsAdd == nj.IsAdd))
            .ToArray();
        var toRemove = joins.Where(j => !newJoins.Any(nj => j.UnitStateId == nj.UnitStateId && j.IsAdd == nj.IsAdd))
                                   .ToArray();
        
        await _db.StatesToRoutes.AddRangeAsync(toAdd);
        _db.StatesToRoutes.RemoveRange(toRemove);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// "Updates" a Route Operation by creating a new version of and applying an Action which modifies the original.
    /// Returns the ID of the new version.  This can be used as an alternative to UpdateOp in cases where the route
    /// operation has already been referenced by a product unit.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to update</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="stateChanges"></param>
    /// <returns>the ID of the newly added route operation</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the id cannot be found</exception>
    public async Task<int> IncrementOpVersion(int id, Action<TRouteOperation> modifyAction, 
        OpStateChanges<TUnitState> stateChanges)
    {
        var item = await _db.RouteOperations.AsNoTracking().FirstOrDefaultAsync(o => o.Id == id);
        if (item is null) throw new KeyNotFoundException();
        var root = item.RootId;
        var version = item.RootVersion + 1;
        
        modifyAction.Invoke(item);
        item.Id = 0;
        item.RootId = root;
        item.RootVersion = version;
        item.ThrowIfInvalid();
        await _db.AddAsync(item);
        await _db.SaveChangesAsync();

        var newJoins = GetJoins(item.Id, stateChanges);
        await _db.StatesToRoutes.AddRangeAsync(newJoins);
        await _db.SaveChangesAsync();

        return item.Id;
    }

    /// <summary>
    /// "Updates" a route operation by either using UpdateOp if the operation is free to be edited, or
    /// IncrementOpVersion if it is not.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to update</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="stateChanges"></param>
    /// <returns>the ID of the route operation, will be the same as id if it was updated in place</returns>
    public async Task<int> UpdateOrIncrement(int id, Action<TRouteOperation> modifyAction, 
        OpStateChanges<TUnitState> stateChanges)
    {
        if (await IsOpLocked(id))
        {
            return await IncrementOpVersion(id, modifyAction, stateChanges);
        }

        await UpdateOp(id, modifyAction, stateChanges);
        return id;
    }

    /// <summary>
    /// Deletes a Route Operation by its id. Will fail if the Route Operation has already been referenced by a
    /// Product Unit through a Unit Operation.
    /// </summary>
    /// <param name="id">The ID of the route operation to delete</param>
    /// <exception cref="InvalidOperationException">Thrown if the operation has already been referenced</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public async Task DeleteOp(int id)
    {
        if (await IsOpLocked(id))
            throw new InvalidOperationException(
                "This route operation has already been referenced and cannot be deleted.");
        
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        
        var joins = _db.StatesToRoutes.Where(j => j.RouteOperationId == id).ToArray();
        
        _db.StatesToRoutes.RemoveRange(joins);
        _db.RouteOperations.Remove(item);
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// Sets the `Archived` flag on a Route Operation, effectively deactivating it. This is the alternative to DeleteOp
    /// if the operation has already been referenced.
    /// </summary>
    /// <param name="id">The ID of the route operation to deactivate</param>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public async Task ArchiveOp(int id)
    {
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        item.Archived = true;
        await _db.SaveChangesAsync();
    }
    
    /// <summary>
    /// Deletes or archives a Route Operation depending on whether or not it has already been referenced by a Product
    /// Unit. Preferentially favors deletion if possible, using Archive only if deletion is not possible.
    /// </summary>
    /// <param name="id">The ID of the route operation to deactivate</param>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public async Task DeleteOrArchiveOp(int id)
    {
        if (!await IsOpLocked(id))
        {
            await DeleteOp(id);
            return;
        }

        await ArchiveOp(id);
    }
    
    /// <summary>
    /// Build and retrieve a Route object representing the master route of a given product type.
    /// </summary>
    /// <param name="productTypeId">The ID of the product type to build the route for</param>
    /// <returns>Object representing the master route</returns>
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

            var stateChanges = new OpStateChanges<TUnitState>(adds, removes);
            results.Add(new RouteOpAndStates<TProductType, TUnitState, TRouteOperation>(op, stateChanges));
        }

        return new Route<TProductType, TUnitState, TRouteOperation>(productTypeId, results.ToArray());
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="routeOpId"></param>
    /// <param name="states"></param>
    /// <returns></returns>
    private StateRoute<TProductType, TUnitState, TRouteOperation>[] GetJoins(int routeOpId,
        OpStateChanges<TUnitState> states)
    {
        return states.Adds.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = true, RouteOperationId = routeOpId, UnitStateId = s.Id
        }).Concat(states.Removes.Select(s => new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            IsAdd = false, RouteOperationId = routeOpId, UnitStateId = s.Id
        })).ToArray();
    }

}