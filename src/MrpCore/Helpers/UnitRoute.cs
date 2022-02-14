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
    private readonly IReadOnlyDictionary<int, OpStateChanges<TUnitState>> _stateChanges;

    public UnitRoute(int unitId, TOperationResult[] results, TUnitOperation[] operations, 
        IReadOnlyDictionary<int, OpStateChanges<TUnitState>> stateChanges)
    {
        UnitId = unitId;
        _results = results.OrderBy(o => o.UtcTime).ToArray();
        _operations = operations.ToDictionary(o => o.Id, o => o);
        _stateChanges = stateChanges;
        
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

    public bool HasRouteOp(int routeOpId) => _operations.Values.Any(o => o.RouteOperationId == routeOpId);
    
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
        var routeOperations = _operations.Values.Select(o => o.RouteOperation)
            .ToDictionary(o => o.RootId, o => o);
        
        // If the last operation failed, the next operation is based on the failure behavior of the last operation
        if (!LastResult.Pass)
        {
            NextRouteOperation = lastRoute.FailureBehavior switch
            {
                RouteOpFailure.Retry => lastRoute,
                RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn => routeOperations.Values
                    .Where(o => o.CorrectiveId == lastRoute.RootId)
                    .MinBy(o => o.OpNumber),
                _ => null
            };

            State = WipState.FailedLast;
            return;
        }

        // The last operation passed. What happens next is determined by several things.
        if (lastRoute.AddBehavior.IsStandard())
        {
            NextRouteOperation = routeOperations.Values
                .Where(o => o.AddBehavior.IsStandard() && o.OpNumber > lastRoute.OpNumber)
                .MinBy(o => o.OpNumber);
        }
        else if (lastRoute.AddBehavior is RouteOpAdd.Corrective)
        {
            // Find the next operation in the corrective chain
           NextRouteOperation = routeOperations.Values
                .Where(o => o.CorrectiveId == lastRoute.CorrectiveId && o.OpNumber > lastRoute.OpNumber)
                .MinBy(o => o.OpNumber);

           if (NextRouteOperation is not null) return;
            
           // If the corrective chain has ended we follow the behavior of the original failed operation
            var failedOp = routeOperations[lastRoute.CorrectiveId];
            NextRouteOperation = failedOp!.FailureBehavior switch
            {
                RouteOpFailure.CorrectiveProceed => routeOperations.Values
                    .Where(o => o.AddBehavior.IsStandard() && o.OpNumber > failedOp.OpNumber)
                    .MinBy(o => o.OpNumber),
                RouteOpFailure.CorrectiveReturn => failedOp,
                _ => throw new ArgumentOutOfRangeException()
            };
        }

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