using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore.Services;

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

    private readonly IMesUpdater _updater;
    
    protected MesManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> db, IMesUpdater updater)
    {
        _db = db;
        _updater = updater;
        RouteManager =
            new MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult>(_db, _updater);
        
        UnitManager =
            new MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult>(_db, RouteManager, _updater);

        ToolManager =
            new MesToolingManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
                TOperationResult>(_db, _updater);
    }

    public MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        RouteManager { get; }
    
    public MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        UnitManager { get; }
    
    public MesToolingManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        ToolManager { get; }

    public IQueryable<Namespace> Namespaces => _db.Namespaces; 
    public IQueryable<TProductType> ProductTypes => _db.Types;
    public ValueTask<TProductType?> ProductTypeById(int id) => _db.Types.FindAsync(id);
    public IQueryable<TUnitState> UnitStates => _db.States;

    public ValueTask<TUnitState?> UnitStateById(int stateId) => _db.States.FindAsync(stateId);

    public async Task UpdateState(int stateId, Action<TUnitState> modifyAction)
    {
        var target = await _db.States.FindAsync(stateId);
        if (target is null) throw new KeyNotFoundException();

        var locked = await StateHasBeenReferenced(stateId);
        var blocks = target.BlocksCompletion;
        var terminates = target.TerminatesRoute;
        
        modifyAction.Invoke(target);

        if (locked && (blocks != target.BlocksCompletion || terminates != target.TerminatesRoute))
        {
            throw new InvalidOperationException(
                "This state has been referenced, its functional parameters cannot be changed.");
        }

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
    
    public async Task<int> CreateProductType(TProductType newItem)
    {
        await _db.Types.AddAsync(newItem);
        await _db.SaveChangesAsync();
        _updater.UpdateRoute(ChangeType.Created, newItem.Id);
        return newItem.Id;
    }

    public async Task<int> CreateUnitState(TUnitState newItem)
    {
        await _db.States.AddAsync(newItem);
        await _db.SaveChangesAsync();
        _updater.UpdateStates(ChangeType.Created, newItem.Id);
        return newItem.Id;
    }

    public async Task<NamespaceData<TUnitState, TProductType>> GetNamespaceData(int? namespaceId)
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

        return new NamespaceData<TUnitState, TProductType>(productTypes, states, toolTypes);
    }

    public async Task<TProductUnit[]> GetMaterialOptions(int typeId, int? quantity)
    {
        var qty = quantity ?? 0;
        
        var candidates = await _db.Units.AsNoTracking()
            .Where(u => u.ProductTypeId == typeId && !u.Archived && u.Quantity >= qty)
            .ToArrayAsync();

        var ids = candidates.Select(c => c.Id).ToHashSet();
        var consumed = await _db.MaterialClaims.AsNoTracking()
            .Where(x => ids.Contains(x.ProductUnitId))
            .GroupBy(x => x.ProductUnitId)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.QuantityConsumed ?? 0) })
            .ToDictionaryAsync(x => x.Id, x => x.Sum);
        
        foreach (var id in ids.Where(id => !consumed.ContainsKey(id))) consumed[id] = 0;

        return candidates.Where(c => c.Quantity - consumed[c.Id] >= qty).ToArray();
    }

    public async Task<Tool[]> GetToolOptions(int toolTypeId, int? capacity)
    {
        var candidates = await _db.Tools.AsNoTracking()
            .Where(t => !t.Retired && t.TypeId == toolTypeId)
            .ToArrayAsync();

        var ids = candidates.Select(c => c.Id).ToHashSet();
        var consumed = await _db.ToolClaims.AsNoTracking()
            .Where(x => !x.Released && ids.Contains(x.ToolId))
            .GroupBy(x => x.ToolId)
            .Select(g => new { Id = g.Key, Sum = g.Sum(x => x.CapacityTaken ?? 0) })
            .ToDictionaryAsync(x => x.Id, x => x.Sum);

        foreach (var id in ids.Where(id => !consumed.ContainsKey(id))) consumed[id] = 0;
        
        return candidates.Where(c => c.Capacity - consumed[c.Id] >= (capacity ?? 0)).ToArray();
    }

}