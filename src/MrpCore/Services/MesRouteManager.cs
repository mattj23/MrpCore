using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore.Services;

using QtyFunc = Func<HashSet<int>, IReadOnlyDictionary<int, double>>;

/// <summary>
///     Manager class which specifically handles operations related to Route Operations/master routes.
/// </summary>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TProductUnit"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TUnitOperation"></typeparam>
/// <typeparam name="TOperationResult"></typeparam>
/// <typeparam name="TToolClaim"></typeparam>
/// <typeparam name="TToolType"></typeparam>
/// <typeparam name="TTool"></typeparam>
/// <typeparam name="TToolRequirement"></typeparam>
public class MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
    TToolType, TTool, TToolClaim, TToolRequirement>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>, new()
    where TOperationResult :
    OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>, new()
    where TToolType : ToolTypeBase
    where TToolRequirement : ToolRequirementBase
    where TTool : ToolBase<TToolType>
    where TToolClaim : ToolClaimBase<TToolType, TTool>
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> _db;

    private readonly IMesUpdater _updater;

    public MesRouteManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
            TToolType, TTool, TToolClaim, TToolRequirement> db, IMesUpdater updater)
    {
        _db = db;
        _updater = updater;
    }

    /// <summary>
    ///     Queries to determine if any TUnitOperation references the Route Operation with the specified ID. If the
    ///     operation has been referenced it is no longer editable.
    /// </summary>
    /// <param name="routeOperationId">The ID of the operation being checked</param>
    /// <returns>true if the route operation cannot be edited, false if it is safe to edit</returns>
    public Task<bool> IsOpLocked(int routeOperationId)
    {
        return _db.UnitOperations.AnyAsync(o => o.RouteOperationId == routeOperationId);
    }

    /// <summary>
    ///     Get the highest version of a route operation by its root ID, regardless of whether it is active/archived.
    /// </summary>
    /// <param name="rootId"></param>
    /// <returns></returns>
    public async Task<TRouteOperation?> GetByRootId(int rootId)
    {
        return (await _db.RouteOperations.Where(o => o.RootId == rootId).ToArrayAsync())
            .MaxBy(o => o.RootVersion);
    }

    /// <summary>
    ///     Add a new Route Operation to the data store. This will automatically set the RootId and RootVersion. Do not
    ///     use this method to add an already existing operation.
    /// </summary>
    /// <param name="operation">A new TRouteOperation to be added to the data store</param>
    /// <param name="states"></param>
    /// <param name="toolRequirements"></param>
    /// <param name="materialRequirements"></param>
    /// <returns>The integer ID of the new added operation</returns>
    public virtual async Task<int> AddOp(TRouteOperation operation, StateRelations<TUnitState> states,
        TToolRequirement[] toolRequirements, MaterialRequirement[] materialRequirements)
    {
        operation.Id = 0;
        operation.ThrowIfInvalid();

        await _db.RouteOperations.AddAsync(operation);
        await _db.SaveChangesAsync();

        operation.RootId = operation.Id;
        operation.RootVersion = 0;

        var joins = GetJoins(operation.Id, states);
        await _db.StatesToRoutes.AddRangeAsync(joins);

        await AddRequirements(operation.Id, toolRequirements, materialRequirements);
        await _db.SaveChangesAsync();
        _updater.UpdateRoute(ChangeType.Updated, operation.ProductTypeId);

        return operation.Id;
    }

    /// <summary>
    ///     Adds a new corrective operation to a parent route operation with corrective failure behavior. Handles the
    ///     setting of the internal `CorrectiveId` and `AddBehavior`.
    /// </summary>
    /// <param name="operation"></param>
    /// <param name="parentId"></param>
    /// <param name="states"></param>
    /// <param name="toolRequirements"></param>
    /// <param name="materialRequirements"></param>
    /// <returns></returns>
    /// <exception cref="KeyNotFoundException"></exception>
    /// <exception cref="InvalidOperationException"></exception>
    public virtual async Task<int> AddCorrectiveOp(TRouteOperation operation, int parentId,
        StateRelations<TUnitState> states,
        TToolRequirement[] toolRequirements, MaterialRequirement[] materialRequirements)
    {
        // Check for a valid parent
        var parent = await _db.RouteOperations.FindAsync(parentId);
        if (parent is null) throw new KeyNotFoundException();
        if (parent.FailureBehavior is not (RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn))
            throw new InvalidOperationException(
                "A corrective operation must be added to a parent with corrective failure behavior");

        operation.CorrectiveId = parent.RootId;
        operation.AddBehavior = RouteOpAdd.Corrective;

        return await AddOp(operation, states, toolRequirements, materialRequirements);
    }

    /// <summary>
    ///     Modifies a Route Operation by fetching it from the data store and invoking an Action on it. The operation will
    ///     throw an exception if the Route Operation is already referenced. Use IsOpLocked(id) to determine ahead of time
    ///     if an operation is safe to modify. The addedStates and removedStates arguments will replace the existing
    ///     relations for the Route Operation.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to edit</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="states"></param>
    /// <param name="toolRequirements"></param>
    /// <param name="materialRequirements"></param>
    /// <exception cref="InvalidOperationException">Thrown if the Route Operation is already referenced by a product unit</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the operation ID cannot be found</exception>
    public virtual async Task UpdateOp(int id, Action<TRouteOperation> modifyAction, StateRelations<TUnitState> states,
        TToolRequirement[] toolRequirements, MaterialRequirement[] materialRequirements)
    {
        if (await IsOpLocked(id))
            throw new InvalidOperationException(
                "This route operation has already been referenced and cannot be edited.");

        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();

        modifyAction.Invoke(item);
        item.ThrowIfInvalid();

        var existingToolReqs = await _db.ToolRequirements.Where(r => r.RouteOperationId == id).ToArrayAsync();
        var existingMaterialReqs = await _db.MaterialRequirements.Where(r => r.RouteOperationId == id).ToArrayAsync();
        _db.ToolRequirements.RemoveRange(existingToolReqs);
        _db.MaterialRequirements.RemoveRange(existingMaterialReqs);

        var existingJoins = _db.StatesToRoutes.Where(j => j.RouteOperationId == id).ToArray();
        _db.StatesToRoutes.RemoveRange(existingJoins);

        var newJoins = GetJoins(id, states);
        await _db.StatesToRoutes.AddRangeAsync(newJoins);

        await AddRequirements(id, toolRequirements, materialRequirements);
        await _db.SaveChangesAsync();
        _updater.UpdateRoute(ChangeType.Updated, item.ProductTypeId);
    }

    /// <summary>
    ///     "Updates" a Route Operation by creating a new version of and applying an Action which modifies the original.
    ///     Returns the ID of the new version.  This can be used as an alternative to UpdateOp in cases where the route
    ///     operation has already been referenced by a product unit.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to update</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="states"></param>
    /// <param name="toolRequirements"></param>
    /// <param name="materialRequirements"></param>
    /// <returns>the ID of the newly added route operation</returns>
    /// <exception cref="KeyNotFoundException">Thrown if the id cannot be found</exception>
    public virtual async Task<int> IncrementOpVersion(int id, Action<TRouteOperation> modifyAction,
        StateRelations<TUnitState> states, TToolRequirement[] toolRequirements,
        MaterialRequirement[] materialRequirements)
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

        var newJoins = GetJoins(item.Id, states);
        await _db.StatesToRoutes.AddRangeAsync(newJoins);
        await AddRequirements(item.Id, toolRequirements, materialRequirements);

        await _db.SaveChangesAsync();
        _updater.UpdateRoute(ChangeType.Updated, item.ProductTypeId);

        return item.Id;
    }

    /// <summary>
    ///     "Updates" a route operation by either using UpdateOp if the operation is free to be edited, or
    ///     IncrementOpVersion if it is not.
    /// </summary>
    /// <param name="id">The integer ID of the route operation to update</param>
    /// <param name="modifyAction">An Action which modifies the route operation in some way</param>
    /// <param name="states"></param>
    /// <param name="toolRequirements"></param>
    /// <param name="materialRequirements"></param>
    /// <returns>the ID of the route operation, will be the same as id if it was updated in place</returns>
    public virtual async Task<int> UpdateOrIncrement(int id, Action<TRouteOperation> modifyAction,
        StateRelations<TUnitState> states, TToolRequirement[] toolRequirements,
        MaterialRequirement[] materialRequirements)
    {
        if (await IsOpLocked(id))
            return await IncrementOpVersion(id, modifyAction, states, toolRequirements, materialRequirements);

        await UpdateOp(id, modifyAction, states, toolRequirements, materialRequirements);
        return id;
    }

    /// <summary>
    ///     Deletes a Route Operation by its id. Will fail if the Route Operation has already been referenced by a
    ///     Product Unit through a Unit Operation.
    /// </summary>
    /// <param name="id">The ID of the route operation to delete</param>
    /// <exception cref="InvalidOperationException">Thrown if the operation has already been referenced</exception>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public virtual async Task DeleteOp(int id)
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

        _updater.UpdateRoute(ChangeType.Updated, item.ProductTypeId);
    }

    /// <summary>
    ///     Sets the `Archived` flag on a Route Operation, effectively deactivating it. This is the alternative to DeleteOp
    ///     if the operation has already been referenced.
    /// </summary>
    /// <param name="id">The ID of the route operation to deactivate</param>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public virtual async Task ArchiveOp(int id)
    {
        var item = await _db.RouteOperations.FindAsync(id);
        if (item is null) throw new KeyNotFoundException();
        item.Archived = true;
        await _db.SaveChangesAsync();

        _updater.UpdateRoute(ChangeType.Updated, item.ProductTypeId);
    }

    /// <summary>
    ///     Deletes or archives a Route Operation depending on whether or not it has already been referenced by a Product
    ///     Unit. Preferentially favors deletion if possible, using Archive only if deletion is not possible.
    /// </summary>
    /// <param name="id">The ID of the route operation to deactivate</param>
    /// <exception cref="KeyNotFoundException">Thrown if the route ID cannot be found</exception>
    public virtual async Task DeleteOrArchiveOp(int id)
    {
        if (!await IsOpLocked(id))
        {
            await DeleteOp(id);
            return;
        }

        await ArchiveOp(id);
    }

    /// <summary>
    ///     Build and retrieve a Route object representing the master route of a given product type.
    /// </summary>
    /// <param name="productTypeId">The ID of the product type to build the route for</param>
    /// <returns>Object representing the master route</returns>
    public virtual async Task<Route<TProductType, TUnitState, TRouteOperation, TToolRequirement>> GetRoute(int productTypeId)
    {
        var routeOps = await _db.RouteOperations.AsNoTracking()
            .Where(r => r.ProductTypeId == productTypeId)
            .ToListAsync();

        var routeOpIds = routeOps.Select(r => r.Id).ToHashSet();

        var results = new List<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>>();
        foreach (var id in routeOpIds) results.Add(await GetOpAndData(id));

        return new Route<TProductType, TUnitState, TRouteOperation, TToolRequirement>(productTypeId, results.ToArray());
    }

    public virtual async Task<StateRelations<TUnitState>> GetStates(int routeOpId)
    {
        var joins = await _db.StatesToRoutes.AsNoTracking()
            .Where(s => s.RouteOperationId == routeOpId)
            .Include(s => s.State)
            .ToListAsync();
        return new StateRelations<TUnitState>(
            joins.Where(j => j.Relation == OpRelation.Add).Select(j => j.State),
            joins.Where(j => j.Relation == OpRelation.Remove).Select(j => j.State),
            joins.Where(j => j.Relation == OpRelation.Needs).Select(j => j.State),
            joins.Where(j => j.Relation == OpRelation.BlockedBy).Select(j => j.State)
        );
    }

    public virtual async Task<IReadOnlyCollection<RequirementData>> GetRequirementsFor(int routeOpId, bool pass,
        Func<HashSet<int>, Dictionary<int, double>>? additionalMaterialConsumption = null)
    {
        var data = await GetOpAndData(routeOpId);
        var results = new List<RequirementData>();

        foreach (var mat in data.MaterialRequirements)
        {
            if (!pass && !mat.ConsumedOnFailure) continue;
            var reqType = await _db.Types.AsNoTracking().FirstOrDefaultAsync(x => x.Id == mat.ProductTypeId);

            var title = $"Material Requirement: {reqType?.Name}, quantity {mat.Quantity ?? 0}";
            var options = (await GetMaterialOptions(mat.ProductTypeId, mat.Quantity))
                .Select(s => new RequirementData.Option(s.Id, s.ToString(), s))
                .ToArray();
            results.Add(new RequirementData(mat.Id, title, RequirementData.ReqType.Material, options));
        }

        foreach (var toolReq in data.ToolRequirements)
        {
            if (toolReq.Type is ToolRequirementType.Released) continue;
            var capacity = toolReq.Type is ToolRequirementType.UsedOnly ? 0 : toolReq.CapacityTaken ?? 0;

            var reqType = await _db.ToolTypes.AsNoTracking().FirstOrDefaultAsync(x => x.Id == toolReq.ToolTypeId);

            var title = $"Tooling Requirement: {reqType?.Name}";
            if (capacity > 0) title += $", {capacity} capacity";

            var options = (await GetToolOptions(toolReq.ToolTypeId, capacity))
                .Select(s => new RequirementData.Option(s.Id, s.ToString(), s))
                .ToArray();
            results.Add(new RequirementData(toolReq.Id, title, RequirementData.ReqType.Tool, options));
        }

        return results;
    }

    protected virtual async Task<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> GetOpAndData(
        int routeOpId)
    {
        var op = await _db.RouteOperations.FindAsync(routeOpId);
        if (op is null) throw new KeyNotFoundException();

        var states = await GetStates(routeOpId);
        var toolReqs = await _db.ToolRequirements.Where(r => r.RouteOperationId == routeOpId).ToArrayAsync();
        var materialReqs = await _db.MaterialRequirements.Where(r => r.RouteOperationId == routeOpId).ToArrayAsync();

        return new RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>(op, states, toolReqs,
            materialReqs);
    }

    /// <summary>
    /// </summary>
    /// <param name="routeOpId"></param>
    /// <param name="states"></param>
    /// <returns></returns>
    private IReadOnlyCollection<StateRoute<TProductType, TUnitState, TRouteOperation>> GetJoins(int routeOpId,
        StateRelations<TUnitState> states)
    {
        var collection = new List<StateRoute<TProductType, TUnitState, TRouteOperation>>();
        collection.AddRange(states.Adds.Select(s => ToJoin(s, OpRelation.Add, routeOpId)));
        collection.AddRange(states.Removes.Select(s => ToJoin(s, OpRelation.Remove, routeOpId)));
        collection.AddRange(states.Needs.Select(s => ToJoin(s, OpRelation.Needs, routeOpId)));
        collection.AddRange(states.BlockedBy.Select(s => ToJoin(s, OpRelation.BlockedBy, routeOpId)));
        return collection;
    }

    private StateRoute<TProductType, TUnitState, TRouteOperation> ToJoin(TUnitState state,
        OpRelation relation, int routeOpId)
    {
        return new StateRoute<TProductType, TUnitState, TRouteOperation>
        {
            Relation = relation,
            RouteOperationId = routeOpId,
            UnitStateId = state.Id
        };
    }

    protected virtual async Task AddRequirements(int routeOpId, TToolRequirement[] toolRequirements,
        MaterialRequirement[] materialRequirements)
    {
        foreach (var req in toolRequirements)
        {
            req.Id = 0;
            req.RouteOperationId = routeOpId;
        }

        await _db.ToolRequirements.AddRangeAsync(toolRequirements);

        foreach (var req in materialRequirements)
        {
            req.Id = 0;
            req.RouteOperationId = routeOpId;
        }

        await _db.MaterialRequirements.AddRangeAsync(materialRequirements);
    }


    protected virtual async Task<TProductUnit[]> GetMaterialOptions(int typeId, double? quantity)
    {
        var qty = quantity ?? 0.0;

        // Find all non-archived units of the given type who had an actual quantity assigned
        var candidates = await _db.Units.AsNoTracking()
            .Where(u => u.ProductTypeId == typeId && !u.Archived && u.Quantity != null)
            .ToArrayAsync();

        // Now, for each candidate unit, find the amount consumed by material claims
        var ids = candidates.Select(c => c.Id).ToHashSet();
        var consumed = await _db.MaterialClaims.AsNoTracking()
            .Where(x => ids.Contains(x.ProductUnitId))
            .GroupBy(x => x.ProductUnitId)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.Quantity) })
            .ToDictionaryAsync(x => x.Id, x => x.Sum);

        foreach (var id in ids.Where(id => !consumed.ContainsKey(id))) consumed[id] = 0;

        return candidates.Where(c => c.Quantity - consumed[c.Id] >= qty).ToArray();
    }

    protected virtual async Task<TTool[]> GetToolOptions(int toolTypeId, int? capacity)
    {
        var candidates = await _db.Tools.AsNoTracking()
            .Where(t => !t.Retired && t.TypeId == toolTypeId)
            .ToArrayAsync();

        var ids = candidates.Select(c => c.Id).ToHashSet();
        var consumed = await _db.ToolClaims.AsNoTracking()
            .Where(x => !x.Released && ids.Contains(x.ToolId))
            .GroupBy(x => x.ToolId)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.CapacityTaken) })
            .ToDictionaryAsync(x => x.Id, x => x.Sum);

        foreach (var id in ids.Where(id => !consumed.ContainsKey(id))) consumed[id] = 0;

        return candidates.Where(c => c.Capacity - consumed[c.Id] >= (capacity ?? 0)).ToArray();
    }
}