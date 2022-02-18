using Microsoft.EntityFrameworkCore;
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

    }

    public MesRouteManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        RouteManager { get; }
    
    public MesUnitManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
        UnitManager { get; }

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

}