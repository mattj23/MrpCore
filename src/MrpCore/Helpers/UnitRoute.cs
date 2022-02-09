using MrpCore.Models;

namespace MrpCore.Helpers;
/* Determining the WIP history and future for a part
 *
 * Operations which already have results on them are effectively ordered by the results, rather than any other
 * mechanism. Operations which have successful results may be removed from the to-do list of operations (is this true
 * with the corrective looping mechanism?).
 *
 * For operations which have not been attempted, they appear in the to-do list in the sequence of their operation
 * number.  For operations which have failed, what happens next is based on the failure behavior of the master route
 * operation.  Operations with permissible retry are simply stuck on that operation until they get a passing attempt.
 * Operations with corrective chains need to load the chain into the unit's route and the next operation is the
 * first operation in the chain.
 *
 * At the end of a corrective chain, there has to be additional special behavior depending on whether we retry the
 * original failing operation or move on.
 *
 * The main properties of a WIP state are:
 *  > Is the unit completed?
 *  > If not, what's the next operation that needs to be attempted?
 *  > What are the currently active explicit states?
 *  > What was the result of the last attempted operation
 *
 *
 * In the old system:
 */

/// <summary>
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
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>, new()
    where TOperationResult : OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
{
    private readonly int _unitId;
    private readonly TOperationResult[] _results;
    private readonly TUnitOperation[] _operations;

    public UnitRoute(int unitId, TOperationResult[] results, TUnitOperation[] operations)
    {
        _unitId = unitId;
        _results = results;
        _operations = operations;
    }

    private void Calculate()
    {
        
    }
    
}