using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore.Services;

public class MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
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
    where TToolClaim : ToolClaimBase<TToolType, TTool>, new()
{
    private readonly MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> _db;

    private readonly MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
        TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> _routes;

    protected readonly IMesUpdater Updater;

    public MesUnitManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> db,
        MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> routes, IMesUpdater updater)
    {
        _db = db;
        _routes = routes;
        Updater = updater;
    }

    public IQueryable<TProductUnit> Units => _db.Units.AsNoTracking();

    public ValueTask<TProductUnit?> GetUnit(int unitId)
    {
        return _db.Units.FindAsync(unitId);
    }

    public Task<Dictionary<int, TProductUnit>> GetUnitsByIds(HashSet<int> ids)
    {
        return _db.Units.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Include(t => t.Type)
            .ToDictionaryAsync(t => t.Id, t => t);
    }

    /// <summary>
    ///     Adds a new product unit and creates its unit route from the product type's master route
    /// </summary>
    /// <param name="newUnit"></param>
    /// <param name="modifyOperations"></param>
    /// <param name="modifyUnit"></param>
    public async Task<TProductUnit> AddUnit(TProductUnit newUnit, Action<TUnitOperation[]>? modifyOperations = null,
        Action<TProductUnit>? modifyUnit = null)
    {
        // Verify that the route has at least one default step before allowing the unit to be added
        var route = await _routes.GetRoute(newUnit.ProductTypeId);
        if (!route.DefaultOperations.Any())
            throw new InvalidOperationException(
                "This product type has no default route operations, so a route cannot be constructed");

        newUnit.Id = 0;
        if (newUnit.CreatedUtc == default) newUnit.CreatedUtc = DateTime.UtcNow;

        await _db.Units.AddAsync(newUnit);
        await _db.SaveChangesAsync();

        if (modifyUnit is not null)
        {
            modifyUnit.Invoke(newUnit);
            await _db.SaveChangesAsync();
        }

        var operations = route.DefaultOperations.Select(o => new TUnitOperation
        {
            ProductUnitId = newUnit.Id,
            RouteOperationId = o.Id
        }).ToArray();

        modifyOperations?.Invoke(operations);
        await _db.UnitOperations.AddRangeAsync(operations);
        await _db.SaveChangesAsync();
        Updater.UpdateUnit(ChangeType.Created, newUnit.Id);
        return newUnit;
    }

    public async
        Task<UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
            TToolRequirement>>
        GetUnitRoute(int unitId)
    {
        var target = await _db.Units.AsNoTracking()
            .Include(u => u.Type)
            .FirstOrDefaultAsync(u => u.Id == unitId);

        if (target is null) throw new KeyNotFoundException();

        var unitOps = await _db.UnitOperations.AsNoTracking()
            .Where(o => o.ProductUnitId == unitId)
            .Include(o => o.RouteOperation)
            .ToArrayAsync();

        var routeOps = unitOps.Select(o => o.RouteOperationId).ToHashSet();
        var changes = new Dictionary<int, StateRelations<TUnitState>>();
        foreach (var opId in routeOps)
            changes.Add(opId, await _routes.GetStates(opId));

        var opIds = unitOps.Select(o => o.Id).ToHashSet();
        var results = await _db.OperationResults.AsNoTracking().Where(r => opIds.Contains(r.UnitOperationId))
            .ToArrayAsync();

        var consumed = await GetQtyConsumed(unitId);

        return new UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
            TToolRequirement>(
            target, results, unitOps, changes, consumed);
    }

    protected virtual Task<double> GetQtyConsumed(int unitId)
    {
        return _db.MaterialClaims.AsNoTracking()
            .Where(c => c.ProductUnitId == unitId)
            .SumAsync(c => c.Quantity);
    }

    public async Task AddOpToRoute(int unitId, int routeOpId, Action<TUnitOperation>? modifyOperation = null)
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
            operation = new TUnitOperation { ProductUnitId = unitId, RouteOperationId = routeOpId };
            await _db.UnitOperations.AddAsync(operation);
        }

        modifyOperation?.Invoke(operation);
        await _db.SaveChangesAsync();
        Updater.UpdateUnit(ChangeType.Updated, unitId);
    }

    protected async Task SetArchiveState(int unitId, bool archived, bool skipUpdate = false)
    {
        var target = await _db.Units.FindAsync(unitId);
        if (target is null) throw new KeyNotFoundException();

        if (target.Archived == archived) return;

        target.Archived = archived;
        await _db.SaveChangesAsync();
        if (!skipUpdate)
            Updater.UpdateUnit(ChangeType.Updated, unitId);
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
            operation = new TUnitOperation { ProductUnitId = unitId, RouteOperationId = routeOpId };
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

        // Check if we terminated the route
        var terminated = await _db.StatesToRoutes.AsNoTracking()
            .Include(s => s.State)
            .AnyAsync(s =>
                s.Relation == OpRelation.Add && s.RouteOperationId == routeOpId && s.State.TerminatesRoute);
        if (terminated)
        {
            var unitRoute = await GetUnitRoute(unitId);
            await ReleaseAllToolClaims(unitRoute.Results.Select(r => r.Id).ToHashSet(), result.Id);
            await SetArchiveState(unitId, true, true);
        }

        Updater.UpdateUnit(ChangeType.Updated, unitId);
    }

    public async Task<TOperationResult> ApplyResult(int unitId, int opId, TOperationResult result,
        RequirementSelect[]? selects = null,
        Action<TUnitOperation[]>? modifyCorrective = null,
        Action<TUnitOperation>? modifySpecial = null,
        UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
            TToolRequirement>? unitRoute = null)
    {
        unitRoute ??= await GetUnitRoute(unitId);

        if (unitRoute.NextOperation?.Id != opId)
            throw new ArgumentException("The unit operation ID specified is not the next operation which needs to " +
                                        "run on this unit");

        // Verify requirements
        var routeOpId = unitRoute.NextOperation.RouteOperationId;
        var requirements = await _routes.GetRequirementsFor(routeOpId, result.Pass);
        selects ??= Array.Empty<RequirementSelect>();
        if (!ValidateSelects(selects, requirements))
            throw new InvalidOperationException("Selects don't match requirements for this operation");


        // Prepare and post the result
        result.Id = 0;
        result.UnitOperationId = opId;
        if (result.UtcTime == default)
            result.UtcTime = DateTime.UtcNow;

        await _db.OperationResults.AddAsync(result);
        await _db.SaveChangesAsync();

        // Apply any requirements
        await CreateClaims(result.Id, selects, requirements);

        // Release any tools
        var resultIds = unitRoute.Results.Select(r => r.Id).ToHashSet();
        await ReleaseTools(routeOpId, result.Id, resultIds);

        // Exit if the operation passed
        if (result.Pass)
        {
            // Check for completion on consumable parts
            if (unitRoute.RemainingRoute().Length <= 1)
            {
                unitRoute = await GetUnitRoute(unitId);
                if (unitRoute.State is WipState.Complete)
                {
                    var target = await _db.Units.FindAsync(unitId);
                    target!.Quantity = unitRoute.Unit.Type!.UnitQuantity;
                    await _db.SaveChangesAsync();
                }
            }

            // Did we just terminate the route?  If so we need to release all open tool claims
            var terminated = await _db.StatesToRoutes.AsNoTracking()
                .Include(s => s.State)
                .AnyAsync(s =>
                    s.Relation == OpRelation.Add && s.RouteOperationId == routeOpId && s.State.TerminatesRoute);
            if (terminated)
            {
                unitRoute = await GetUnitRoute(unitId);
                await ReleaseAllToolClaims(unitRoute.Results.Select(r => r.Id).ToHashSet(), result.Id);
            }

            Updater.UpdateResult(true, result.Id);
            Updater.UpdateUnit(ChangeType.Updated, unitId);

            return result;
        }

        // If the operation failed we may need to perform additional actions based on the route operation
        var routeOperation = await _db.RouteOperations.FindAsync(unitRoute.NextOperation.RouteOperationId);
        if (routeOperation is null) throw new KeyNotFoundException("Route operation not found");

        // Find and apply any special operations
        if (routeOperation.SpecialFailId is not null && routeOperation.SpecialFailId > 0)
        {
            var special = await _routes.GetByRootId((int)routeOperation.SpecialFailId);
            if (special is null)
                throw new KeyNotFoundException($"Could not find route by root ID {routeOperation.SpecialFailId}");

            await ApplySpecialOp(unitId, special.Id, new TOperationResult(), modifySpecial);
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

        Updater.UpdateResult(false, result.Id);
        Updater.UpdateUnit(ChangeType.Updated, unitId);

        return result;
    }

    public Task<MaterialClaim[]> GetMaterialClaims(int unitId)
    {
        return _db.MaterialClaims.AsNoTracking()
            .Where(c => c.ProductUnitId == unitId)
            .ToArrayAsync();
    }

    public async Task UpdateUnit(int unitId, Action<TProductUnit> modifyAction)
    {
        var target = await _db.Units.FindAsync(unitId);
        if (target is null) throw new KeyNotFoundException();

        modifyAction(target);
        Updater.UpdateUnit(ChangeType.Updated, unitId);
        await _db.SaveChangesAsync();
    }

    public async Task<OperationResultData<TToolType, TTool, TToolClaim>> GetResultData(int resultId)
    {
        var toolClaims = await _db.ToolClaims.AsNoTracking()
            .Where(c => c.ResultId == resultId)
            .Include(c => c.Tool)
            .ToArrayAsync();

        var materialClaims = await _db.MaterialClaims.AsNoTracking()
            .Where(c => c.ResultId == resultId)
            .ToArrayAsync();

        return new OperationResultData<TToolType, TTool, TToolClaim>(toolClaims, materialClaims);
    }

    private async Task ReleaseAllToolClaims(HashSet<int> unitOpResultIds, int? resultId = null)
    {
        var openClaims = await _db.ToolClaims
            .Where(c => !c.Released && unitOpResultIds.Contains(c.ResultId))
            .Include(c => c.Tool)
            .ToArrayAsync();

        foreach (var claim in openClaims)
        {
            claim.Released = true;
            claim.ReleaseId = resultId;
        }

        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// 
    /// </summary>
    /// <param name="routeOperationId"></param>
    /// <param name="resultId">The id of this result operation</param>
    /// <param name="unitOpResultIds">A hashset of previous result operation IDs which may have made tool claims</param>
    private async Task ReleaseTools(int routeOperationId, int resultId, HashSet<int> unitOpResultIds)
    {
        // Are there any releasing tool requirements?
        var releases = await _db.ToolRequirements.AsNoTracking()
            .Where(r => r.RouteOperationId == routeOperationId && r.Type == ToolRequirementType.Released)
            .ToArrayAsync();

        if (!releases.Any()) return;

        // Find all open claims associated with this product unit
        var openClaims = await _db.ToolClaims.AsNoTracking()
            .Where(c => !c.Released && unitOpResultIds.Contains(c.ResultId))
            .Include(c => c.Tool)
            .ToArrayAsync();

        foreach (var release in releases)
        {
            // Find the open claims for this tool type
            var matching = openClaims.Where(c => c.Tool!.TypeId == release.ToolTypeId).ToArray();

            // TODO: Skip? Should we raise an error?
            if (!matching.Any()) continue;

            // TODO: should the release be the oldest? or the most recent? For now it's just the first
            var target = await _db.ToolClaims.FindAsync(matching.First().Id);
            target!.Released = true;
            target!.ReleaseId = resultId;
        }

        await _db.SaveChangesAsync();
    }

    private bool ValidateSelects(IReadOnlyCollection<RequirementSelect> selects,
        IReadOnlyCollection<RequirementData> requirements)
    {
        foreach (var req in requirements)
        {
            // Find the matching select
            var match = selects.FirstOrDefault(s => s.ReqId == req.ReferenceId && s.Type == req.Type);
            if (match is null)
                return false;

            // Now find the option that was in the select
            if (req.Options.All(v => v.Id != match.SelectedId))
                return false;
        }

        return true;
    }

    private async Task CreateClaims(int resultId, IReadOnlyCollection<RequirementSelect> selects,
        IReadOnlyCollection<RequirementData> requirements)
    {
        var updated = false;
        foreach (var req in requirements)
        {
            // Find the matching select
            var match = selects.FirstOrDefault(s => s.ReqId == req.ReferenceId && s.Type == req.Type);
            if (match is null)
                throw new InvalidOperationException("Cannot construct claim, requirement missing");

            var selectedOption = req.Options.FirstOrDefault(o => o.Id == match.SelectedId);
            if (selectedOption is null)
                throw new InvalidOperationException("Cannot construct claim, requirement specified an invalid option");

            switch (req.Type)
            {
                case RequirementData.ReqType.Material:
                    var originalMatReq = await _db.MaterialRequirements.AsNoTracking()
                        .FirstAsync(r => r.Id == req.ReferenceId);
                    await _db.MaterialClaims.AddAsync(new MaterialClaim
                    {
                        ProductUnitId = match.SelectedId,
                        Quantity = originalMatReq.Quantity ?? 0.0,
                        ResultId = resultId
                    });
                    updated = true;
                    break;
                case RequirementData.ReqType.Tool:
                    var originalToolReq = await _db.ToolRequirements.AsNoTracking()
                        .FirstAsync(r => r.Id == req.ReferenceId);
                    await _db.ToolClaims.AddAsync(new TToolClaim
                    {
                        ToolId = match.SelectedId,
                        CapacityTaken = originalToolReq.CapacityTaken ?? 0,
                        ResultId = resultId,
                        Released = originalToolReq.Type is not ToolRequirementType.Occupied
                    });
                    updated = true;
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        if (updated)
            await _db.SaveChangesAsync();
    }
}