﻿using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore.Services;

public class MesToolingManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation,
    TOperationResult,
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
        TOperationResult,
        TToolType, TTool, TToolClaim, TToolRequirement> _db;

    private readonly IMesUpdater _updater;

    public MesToolingManager(
        MesContext<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult,
            TToolType, TTool, TToolClaim, TToolRequirement> db, IMesUpdater updater)
    {
        _db = db;
        _updater = updater;
    }

    public Task<TToolType[]> GetTypes(int? namespaceId)
    {
        return _db.ToolTypes.AsNoTracking().Where(t => t.NamespaceId == namespaceId).ToArrayAsync();
    }

    public Task<TToolType> GetType(int toolTypeId)
    {
        return _db.ToolTypes.AsNoTracking().FirstAsync(x => x.Id == toolTypeId);
    }

    public virtual async Task<bool> TypeHasBeenReferenced(int toolTypeId)
    {
        return await _db.ToolRequirements.AsNoTracking().AnyAsync(r => r.ToolTypeId == toolTypeId) ||
               await _db.Tools.AsNoTracking().AnyAsync(t => t.TypeId == toolTypeId);
    }

    public virtual async Task<int> CreateType(TToolType newType)
    {
        await _db.ToolTypes.AddAsync(newType);
        await _db.SaveChangesAsync();
        _updater.UpdateToolType(ChangeType.Created, newType.Id);
        return newType.Id;
    }

    public virtual async Task UpdateType(int typeId, Action<TToolType> modifyAction)
    {
        var target = await _db.ToolTypes.FindAsync(typeId);
        if (target is null) throw new KeyNotFoundException();

        modifyAction.Invoke(target);
        await _db.SaveChangesAsync();
        _updater.UpdateToolType(ChangeType.Updated, typeId);
    }

    public virtual async Task DeleteType(int typeId)
    {
        var target = await _db.ToolTypes.FindAsync(typeId);
        if (target is null) throw new KeyNotFoundException();

        var locked = await TypeHasBeenReferenced(typeId);
        if (locked)
            throw new InvalidOperationException("Tool Type cannot be deleted after it has already been referenced");

        _db.ToolTypes.Remove(target);
        await _db.SaveChangesAsync();
        _updater.UpdateToolType(ChangeType.Deleted, typeId);
    }

    public virtual Task<TTool[]> GetTools(int? namespaceId)
    {
        return _db.Tools.AsNoTracking()
            .Include(t => t.Type)
            .Where(t => t.Type!.NamespaceId == namespaceId)
            .ToArrayAsync();
    }

    public virtual Task<TTool?> GetTool(int toolId)
    {
        return _db.Tools.AsNoTracking()
            .Include(t => t.Type)
            .FirstOrDefaultAsync(t => t.Id == toolId);
    }

    public virtual Task<bool> ToolHasBeenReferenced(int toolId)
    {
        return _db.ToolClaims.AsNoTracking().AnyAsync(t => t.ToolId == toolId);
    }

    public virtual async Task<int> CreateTool(TTool tool)
    {
        await _db.Tools.AddAsync(tool);
        await _db.SaveChangesAsync();
        _updater.UpdateTool(ChangeType.Created, tool.Id);
        return tool.Id;
    }

    public virtual async Task UpdateTool(int toolId, Action<TTool> modifyAction)
    {
        var target = await _db.Tools.FindAsync(toolId);
        if (target is null) throw new KeyNotFoundException();

        // var locked = await ToolHasBeenReferenced(toolId);

        modifyAction.Invoke(target);
        await _db.SaveChangesAsync();
        _updater.UpdateTool(ChangeType.Updated, toolId);
    }

    public virtual async Task DeleteTool(int toolId)
    {
        var target = await _db.Tools.FindAsync(toolId);
        if (target is null) throw new KeyNotFoundException();

        var locked = await ToolHasBeenReferenced(toolId);
        if (locked)
            throw new InvalidOperationException("Tool cannot be deleted after it has already been referenced");

        _db.Tools.Remove(target);
        await _db.SaveChangesAsync();
        _updater.UpdateTool(ChangeType.Deleted, toolId);
    }
}