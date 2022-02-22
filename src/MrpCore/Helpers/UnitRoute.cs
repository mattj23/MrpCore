using MrpCore.Models;

namespace MrpCore.Helpers;

/// <summary>
/// Represents a manufacturing route for a specific unit.
/// 
/// </summary>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TProductUnit"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TUnitOperation"></typeparam>
/// <typeparam name="TOperationResult"></typeparam>
public class UnitRoute<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation, TOperationResult>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TProductUnit, TRouteOperation>, new()
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    private readonly TOperationResult[] _results;
    private readonly TProductUnit _unit;
    private readonly IReadOnlyDictionary<int, TUnitOperation> _operations;
    private readonly IReadOnlyDictionary<int, StateRelations<TUnitState>> _stateChanges;
    private readonly MaterialClaim[] _materialClaims;

    public UnitRoute(TProductUnit unit, TOperationResult[] results, TUnitOperation[] operations, 
        IReadOnlyDictionary<int, StateRelations<TUnitState>> stateChanges, MaterialClaim[] materialClaims)
    {
        _unit = unit;
        _results = results.OrderBy(o => o.UtcTime).ToArray();
        _operations = operations.ToDictionary(o => o.Id, o => o);
        _stateChanges = stateChanges;
        _materialClaims = materialClaims;

        foreach (var r in _results)
        {
            r.Operation = _operations[r.UnitOperationId];
        }
        
        RouteOperations = _operations.Values.Select(o => o.RouteOperation)
            .ToDictionary(o => o.RootId, o => o);
        
        // Determine the material quantity left on this unit
        if (Unit.Type!.Consumable && Unit.Quantity > 0)
        {
            RemainingQuantity = _unit.Quantity - (_materialClaims.Select(c => c.QuantityConsumed).Sum() ?? 0);
        }
        
        Calculate();

        NextOperation = _operations.Values.FirstOrDefault(o => o.RouteOperationId == NextRouteOperation?.Id);
    }
    
    public int RemainingQuantity { get; }

    public int UnitId => _unit.Id;

    public TProductUnit Unit => _unit;

    public TRouteOperation? NextRouteOperation { get; private set; }
    
    public TUnitOperation? NextOperation { get; }
    
    public bool IsComplete { get; private set; }

    public IReadOnlyDictionary<int, TUnitOperation> OperationsById => _operations;

    public IReadOnlyCollection<TUnitState> ActiveStates { get; private set; }

    public IReadOnlyCollection<TOperationResult> Results => _results;
    
    public TOperationResult? LastResult { get; private set; }
    
    public WipState State { get; private set; }

    public bool HasRouteOp(int routeOpId) => RouteOperations.ContainsKey(routeOpId);
    
    public IReadOnlyDictionary<int, TRouteOperation> RouteOperations { get; private set; }

    /// <summary>
    /// Determines whether or not an operation that had the given set of state relations would be allowed to run based
    /// on the Needs and BlockedBy collections checked against this unit's active states.
    /// </summary>
    /// <param name="relations"></param>
    /// <returns></returns>
    public bool CanOpRun(StateRelations<TUnitState> relations)
    {
        return !StatesBlockingOp(relations).Any() && !StatesMissingForOp(relations).Any();
    }

    /// <summary>
    /// From a set of state relations, determines which ones in the Needs collection are missing from the active state
    /// of this unit and thus are preventing an associated operation from occuring.
    /// </summary>
    /// <param name="relations"></param>
    /// <returns></returns>
    public IReadOnlyCollection<TUnitState> StatesMissingForOp(StateRelations<TUnitState> relations) =>
        relations.Needs.Where(r => !ActiveStates.Contains(r)).ToArray();

    /// <summary>
    /// From a set of state relations, determines which ones in the BlockedBy collection are currently in the unit's
    /// active states and thus are blocking an associated operation
    /// </summary>
    /// <param name="relations"></param>
    /// <returns></returns>
    public IReadOnlyCollection<TUnitState> StatesBlockingOp(StateRelations<TUnitState> relations) =>
        relations.BlockedBy.Where(r => ActiveStates.Contains(r)).ToArray();
    
    /// <summary>
    /// Calculates and returns what the remaining operations in the route would be assuming that all operation results
    /// are successful from this point forward.
    /// </summary>
    /// <returns></returns>
    public TRouteOperation[] RemainingRoute()
    {
        if (NextRouteOperation is null)
            return Array.Empty<TRouteOperation>();

        var operations = new List<TRouteOperation> {NextRouteOperation};

        var after = OperationAfter(operations.Last());
        while (after is not null)
        {
            operations.Add(after);
            after = OperationAfter(operations.Last());
        }

        return operations.ToArray();
    }

    /// <summary>
    /// Returns true if there is a next operation and that operation is currently being blocked by an active state on
    /// the unit.
    /// </summary>
    /// <returns></returns>
    public bool IsNextOpBlocked()
    {
        if (NextRouteOperation is null) return false;
        return !CanOpRun(_stateChanges[NextRouteOperation.Id]);
    }

    public IReadOnlyCollection<TUnitState> MissingForNextOp()
    {
        if (NextRouteOperation is null) return Array.Empty<TUnitState>();
        return StatesMissingForOp(_stateChanges[NextRouteOperation.Id]);
    }
    
    public IReadOnlyCollection<TUnitState> BlockingNextOp()
    {
        if (NextRouteOperation is null) return Array.Empty<TUnitState>();
        return StatesBlockingOp(_stateChanges[NextRouteOperation.Id]);
    }

    /// <summary>
    /// From a master route, determine which missing standard operations would be allowable to add to this unit's route
    /// </summary>
    /// <param name="master"></param>
    /// <returns></returns>
    public TRouteOperation[] AllowableOptionals(Route<TProductType, TUnitState, TRouteOperation> master)
    {
        if (State is WipState.Terminated) return Array.Empty<TRouteOperation>();
        
        var passedStandard = Results
            .Where(r => r.Operation!.RouteOperation!.AddBehavior.IsStandard() && r.Pass)
            .Select(r => r.Operation!.RouteOperation!.OpNumber)
            .ToArray();
        var lastOpNumber = passedStandard.Any() ? passedStandard.Max() : 0;

        var roots = RouteOperations.Values.Select(r => r.RootId).ToHashSet();
        return master.Standard
            .Where(r => r.Op.OpNumber > lastOpNumber && !roots.Contains(r.Op.RootId))
            .Select(r => r.Op)
            .ToArray();
    }
    
    /// <summary>
    /// From a master route, determine which special operations would be allowable to perform on this unit at the
    /// current time.
    /// </summary>
    /// <param name="master"></param>
    /// <returns></returns>
    public RouteOpAndData<TProductType, TUnitState, TRouteOperation>[] AllowableSpecials(Route<TProductType, TUnitState, TRouteOperation> master)
    {
        if (State is WipState.Terminated) return Array.Empty<RouteOpAndData<TProductType, TUnitState, TRouteOperation>>();

        return master.Special.Where(o => CanOpRun(o.States))
            .ToArray();
    }

    private void Calculate()
    {
        IsComplete = false;
        
        // Compute the currently active states on the unit
        var states = new HashSet<TUnitState>();
        foreach (var result in _results.OrderBy(r => r.UtcTime).Where(r => r.Pass))
        {
            var routeId = result.Operation!.RouteOperationId;
            if (!_stateChanges.ContainsKey(routeId)) continue;

            foreach (var s in _stateChanges[routeId].Adds)
                states.Add(s);

            foreach (var s in _stateChanges[routeId].Removes)
                states.Remove(s);
        }
        ActiveStates = states.ToArray();
        if (ActiveStates.Any(s => s.TerminatesRoute))
        {
            State = WipState.Terminated;
            return;
        }
        
        // Get the last result and figure out what the next operation is
        LastResult = _results.LastOrDefault(o => o.Operation!.RouteOperation!.AddBehavior.NotSpecial());

        // No operation results means this unit is in the initial state
        if (LastResult is null)
        {
            NextRouteOperation = _operations.Values
                .Select(o => o.RouteOperation!)
                .Where(o => o.AddBehavior.IsStandard())
                .MinBy(o => o.OpNumber);
            State = WipState.NotStarted;
            return;
        }

        var lastRoute = _operations[LastResult.UnitOperationId].RouteOperation!;
        
        // If the last operation failed, the next operation is based on the failure behavior of the last operation
        if (!LastResult.Pass)
        {
            NextRouteOperation = lastRoute.FailureBehavior switch
            {
                RouteOpFailure.Retry => lastRoute,
                RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn => RouteOperations.Values
                    .Where(o => o.CorrectiveId == lastRoute.RootId)
                    .MinBy(o => o.OpNumber),
                _ => null
            };

            State = WipState.FailedLast;
            return;
        }

        NextRouteOperation = OperationAfter(lastRoute);

        if (NextRouteOperation is not null)
        {
            State = WipState.InProcess;
        }
        else
        {
            IsComplete = !ActiveStates.Any(s => s.BlocksCompletion);
            State = IsComplete ? WipState.Complete : WipState.Blocked;
            
            // Lastly, check for consumption
            if (State is WipState.Complete && RemainingQuantity <= 0 && Unit.Quantity > 0)
            {
                State = WipState.Consumed;
            }
        }
    }

    private TRouteOperation? OperationAfter(TRouteOperation op)
    {
        if (op.AddBehavior.IsStandard())
        {
            return RouteOperations.Values
                .Where(o => o.AddBehavior.IsStandard() && o.OpNumber > op.OpNumber)
                .MinBy(o => o.OpNumber);
        }
        
        if (op.AddBehavior is RouteOpAdd.Corrective)
        {
            // Find the next operation in the corrective chain
           var next = RouteOperations.Values
                .Where(o => o.CorrectiveId == op.CorrectiveId && o.OpNumber > op.OpNumber)
                .MinBy(o => o.OpNumber);

           if (next is not null) return next;
            
           // If the corrective chain has ended we follow the behavior of the original failed operation
            var failedOp = RouteOperations[op.CorrectiveId];
            return failedOp!.FailureBehavior switch
            {
                RouteOpFailure.CorrectiveProceed => RouteOperations.Values
                    .Where(o => o.AddBehavior.IsStandard() && o.OpNumber > failedOp.OpNumber)
                    .MinBy(o => o.OpNumber),
                RouteOpFailure.CorrectiveReturn => failedOp,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

        throw new NotSupportedException($"Behavior type {op.AddBehavior} is not supported for operation after");
    }
    
}

public enum WipState
{
    NotStarted,
    InProcess,
    FailedLast,
    Terminated,
    Blocked,
    Complete,
    Consumed
}