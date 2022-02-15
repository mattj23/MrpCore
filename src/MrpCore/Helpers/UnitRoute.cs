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
    private readonly IReadOnlyDictionary<int, TUnitOperation> _operations;
    private readonly IReadOnlyDictionary<int, StateRelations<TUnitState>> _stateChanges;

    public UnitRoute(int unitId, TOperationResult[] results, TUnitOperation[] operations, 
        IReadOnlyDictionary<int, StateRelations<TUnitState>> stateChanges)
    {
        UnitId = unitId;
        _results = results.OrderBy(o => o.UtcTime).ToArray();
        _operations = operations.ToDictionary(o => o.Id, o => o);
        _stateChanges = stateChanges;

        foreach (var r in _results)
        {
            r.Operation = _operations[r.UnitOperationId];
        }
        
        RouteOperations = _operations.Values.Select(o => o.RouteOperation)
            .ToDictionary(o => o.RootId, o => o);
        
        Calculate();

        NextOperation = _operations.Values.FirstOrDefault(o => o.RouteOperationId == NextRouteOperation?.Id);
    }
    
    public int UnitId { get; }

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
    Complete
}