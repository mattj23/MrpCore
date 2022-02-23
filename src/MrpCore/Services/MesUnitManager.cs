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
    
    public Task<Dictionary<int, TProductUnit>> GetUnitsByIds(HashSet<int> ids)
    {
        return _db.Units.AsNoTracking()
            .Where(t => ids.Contains(t.Id))
            .Include(t => t.Type)
            .ToDictionaryAsync(t => t.Id, t => t);
    }

    /// <summary>
    /// Adds a new product unit and creates its unit route from the product type's master route
    /// </summary>
    /// <param name="newUnit"></param>
    /// <param name="modifyOperations"></param>
    public async Task AddUnit(TProductUnit newUnit, Action<TUnitOperation[]>? modifyOperations = null,
        Action<TProductUnit>? modifyUnit = null)
    {
        newUnit.Id = 0;
        if (newUnit.CreatedUtc == default) newUnit.CreatedUtc = DateTime.UtcNow;
        
        await _db.Units.AddAsync(newUnit);
        await _db.SaveChangesAsync();

        if (modifyUnit is not null)
        {
            modifyUnit.Invoke(newUnit);
            await _db.SaveChangesAsync();
        }
        
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

        var materialClaims = await _db.MaterialClaims.AsNoTracking()
            .Where(c => c.ProductUnitId == unitId)
            .ToArrayAsync();

        return new UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>(
            target, results, unitOps, changes, materialClaims);
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
        
        // Check if we terminated the route
        var terminated = await _db.StatesToRoutes.AsNoTracking()
            .Include(s => s.State)
            .AnyAsync(s =>
                s.Relation == OpRelation.Add && s.RouteOperationId == routeOpId && s.State.TerminatesRoute);
        if (terminated)
        {
            var unitRoute = await GetUnitRoute(unitId);
            await ReleaseAllToolClaims(unitRoute.Results.Select(r => r.Id).ToHashSet());
        }
            
        _updater.UpdateUnit(ChangeType.Updated, unitId);
    }

    public async Task ApplyResult(int unitId, int opId, TOperationResult result,
        RequirementSelect[]? selects=null,
        Action<TUnitOperation[]>? modifyCorrective=null, 
        Action<TUnitOperation>? modifySpecial = null)
    {
        var unitRoute = await GetUnitRoute(unitId);

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
        await ReleaseTools(routeOpId, resultIds);
        
        // Exit if the operation passed
        if (result.Pass)
        {
            // Check for completion on consumable parts
            if (unitRoute.Unit.Type!.Consumable && unitRoute.RemainingRoute().Length <= 1)
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
                await ReleaseAllToolClaims(unitRoute.Results.Select(r => r.Id).ToHashSet());
            }

            _updater.UpdateResult(true, result.Id);
            _updater.UpdateUnit(ChangeType.Updated, unitId);
            
            return;
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

    public async Task<OperationResultData> GetResultData(int resultId)
    {
        var toolClaims = await _db.ToolClaims.AsNoTracking()
            .Where(c => c.ResultId == resultId)
            .Include(c => c.Tool)
            .ToArrayAsync();

        var materialClaims = await _db.MaterialClaims.AsNoTracking()
            .Where(c => c.ResultId == resultId)
            .ToArrayAsync();

        return new OperationResultData(toolClaims, materialClaims);
    }

    private async Task ReleaseAllToolClaims(HashSet<int> unitOpResultIds)
    {
        var openClaims = await _db.ToolClaims
            .Where(c => !c.Released && unitOpResultIds.Contains(c.ResultId))
            .Include(c => c.Tool)
            .ToArrayAsync();

        foreach (var claim in openClaims)
        {
            claim.Released = true;
        }

        await _db.SaveChangesAsync();
    }
    
    private async Task ReleaseTools(int routeOperationId, HashSet<int> unitOpResultIds)
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
        bool updated = false;
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
                        QuantityConsumed = originalMatReq.Quantity,
                        ResultId = resultId
                    });
                    updated = true;
                    break;
                case RequirementData.ReqType.Tool:
                    var originalToolReq = await _db.ToolRequirements.AsNoTracking() 
                        .FirstAsync(r => r.Id == req.ReferenceId);
                    await _db.ToolClaims.AddAsync(new ToolClaim
                    {
                        ToolId = match.SelectedId,
                        CapacityTaken = originalToolReq.CapacityTaken,
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