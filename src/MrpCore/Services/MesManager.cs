using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore.Services;

public class MesManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
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

    private readonly IMesUpdater _updater;

    protected MesManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement> db, IMesUpdater updater)
    {
        _db = db;
        _updater = updater;
        RouteManager =
            new MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult,
                TToolType, TTool, TToolClaim, TToolRequirement>(_db, _updater);

        UnitManager =
            new MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement>(_db, RouteManager, _updater);

        ToolManager =
            new MesToolingManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult,
                TToolType, TTool, TToolClaim, TToolRequirement>(_db, _updater);
    }

    public virtual MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult,
            TToolType, TTool, TToolClaim, TToolRequirement>
        RouteManager { get; }

    public virtual MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult, TToolType, TTool, TToolClaim, TToolRequirement>
        UnitManager { get; }

    public virtual MesToolingManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
            TOperationResult,
            TToolType, TTool, TToolClaim, TToolRequirement>
        ToolManager { get; }

    public IQueryable<Namespace> Namespaces => _db.Namespaces;
    public IQueryable<TProductType> ProductTypes => _db.Types;

    public ValueTask<TProductType?> ProductTypeById(int id)
    {
        return _db.Types.FindAsync(id);
    }

    public IQueryable<TUnitState> UnitStates => _db.States;

    public ValueTask<TUnitState?> UnitStateById(int stateId)
    {
        return _db.States.FindAsync(stateId);
    }

    public async Task UpdateProductType(int productTypeId, Action<TProductType> modify)
    {
        var target = await _db.Types.FindAsync(productTypeId);
        if (target is null) throw new KeyNotFoundException();

        modify.Invoke(target);
        await _db.SaveChangesAsync();
    }

    public async Task UpdateState(int stateId, Action<TUnitState> modifyAction)
    {
        var target = await _db.States.FindAsync(stateId);
        if (target is null) throw new KeyNotFoundException();

        var locked = await StateHasBeenReferenced(stateId);
        var blocks = target.BlocksCompletion;
        var terminates = target.TerminatesRoute;

        modifyAction.Invoke(target);

        if (locked && (blocks != target.BlocksCompletion || terminates != target.TerminatesRoute))
            throw new InvalidOperationException(
                "This state has been referenced, its functional parameters cannot be changed.");

        await _db.SaveChangesAsync();
    }

    public async Task DeleteState(int stateId)
    {
        var target = await _db.States.FindAsync(stateId);
        if (target is null) throw new KeyNotFoundException();

        var locked = await StateHasBeenReferenced(stateId);
        if (locked)
            throw new InvalidOperationException("State cannot be deleted after it has already been referenced");

        _db.States.Remove(target);
        await _db.SaveChangesAsync();
    }

    public Task<bool> StateHasBeenReferenced(int stateId)
    {
        return _db.StatesToRoutes.AsNoTracking().AnyAsync(j => j.UnitStateId == stateId);
    }

    public Task<Namespace?> GetNamespaceByKey(string key)
    {
        return _db.Namespaces.FirstOrDefaultAsync(n => n.Key == key);
    }

    public Task<bool> HasMultipleNamespaces()
    {
        return _db.Namespaces.AnyAsync();
    }

    public async Task<bool> NamespaceHasBeenReferenced(int namespaceId)
    {
        return await _db.ToolTypes.AsNoTracking().AnyAsync(t => t.NamespaceId == namespaceId) ||
               await _db.States.AsNoTracking().AnyAsync(s => s.NamespaceId == namespaceId) ||
               await _db.Types.AsNoTracking().AnyAsync(t => t.NamespaceId == namespaceId);
    }

    public async Task CreateNamespace(Namespace item)
    {
        await _db.Namespaces.AddAsync(item);
        await _db.SaveChangesAsync();
    }

    public virtual async Task<int> CreateProductType(TProductType newItem)
    {
        await _db.Types.AddAsync(newItem);
        await _db.SaveChangesAsync();
        _updater.UpdateRoute(ChangeType.Created, newItem.Id);
        return newItem.Id;
    }

    public virtual async Task<int> CreateUnitState(TUnitState newItem)
    {
        await _db.States.AddAsync(newItem);
        await _db.SaveChangesAsync();
        _updater.UpdateStates(ChangeType.Created, newItem.Id);
        return newItem.Id;
    }

    public async Task<NamespaceData<TUnitState, TProductType, TToolType>> GetNamespaceData(int? namespaceId)
    {
        var productTypes = await _db.Types.AsNoTracking()
            .Where(p => p.NamespaceId == null || p.NamespaceId == namespaceId)
            .ToArrayAsync();

        var states = await _db.States.AsNoTracking()
            .Where(p => p.NamespaceId == null || p.NamespaceId == namespaceId)
            .ToArrayAsync();

        var toolTypes = await _db.ToolTypes.AsNoTracking()
            .Where(p => p.NamespaceId == null || p.NamespaceId == namespaceId)
            .ToArrayAsync();

        return new NamespaceData<TUnitState, TProductType, TToolType>(productTypes, states, toolTypes);
    }
}