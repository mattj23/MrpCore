﻿using Microsoft.EntityFrameworkCore;
using MrpCore.Helpers;
using MrpCore.Models;

namespace MrpCore;

public class MesManager<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult> 
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>
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

        return new Route<TProductType, TUnitState, TRouteOperation>(results.ToArray());
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
}