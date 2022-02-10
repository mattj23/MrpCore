using MrpCore.Models;

namespace MrpCore.Helpers;

/// <summary>
/// This object represents a "master" manufacturing route for a specific type of product.
///
/// It is constructed from the complete collection of all route operations and computes from it meaning inherent in
/// the complete set, such as operation order, relations between corrective operations and their parent, and special
/// route operations.
/// </summary>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
public class Route<TProductType, TUnitState, TRouteOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
{
    private readonly Dictionary<int, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> _allOperations;
    private readonly Dictionary<int, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> _activeOperations;
    private readonly RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] _special;
    private readonly RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] _standard;
    private readonly TRouteOperation[] _default;

    public Route(int productTypeId, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>[] allOperations)
    {
        ProductTypeId = productTypeId;

        if (allOperations.Any(o => o.Op.ProductTypeId != ProductTypeId))
        {
            throw new InvalidDataException(
                "A Route object must be initialized with all operations from the same part type id");
        }
        
        // Sort all Route Operations into a dictionary by their ID
        _allOperations = allOperations.ToDictionary(o => o.Op.Id, o => o);
        
        // Get the active versions of all operations
        var rootIds = allOperations.Select(o => o.Op.RootId).ToHashSet();
        _activeOperations = new Dictionary<int, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>>();
        foreach (var id in rootIds)
        {
            var latest = allOperations.Where(o => o.Op.RootId == id).MaxBy(o => o.Op.RootVersion);
            if (!latest!.Op.Archived) _activeOperations.Add(id, latest);
        }
        
        _special = _activeOperations.Values
            .Where(o => o.Op.AddBehavior is RouteOpAdd.Special)
            .ToArray();
        
        _standard = _activeOperations.Values
            .Where(o => o.Op.AddBehavior is RouteOpAdd.NotDefault or RouteOpAdd.Default)
            .OrderBy(o => o.Op.OpNumber)
            .ToArray();

        _default = _activeOperations.Values.Select(o => o.Op)
            .Where(o => o.AddBehavior is RouteOpAdd.Default)
            .ToArray();
    }

    /// <summary>
    /// Gets the product type ID associated with this route
    /// </summary>
    public int ProductTypeId { get; }
    
    /// <summary>
    /// Gets a dictionary by which any operation can be accessed by its id, including archived and old versions.
    /// </summary>
    public IReadOnlyDictionary<int, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> AllById=> _allOperations;
    
    /// <summary>
    /// Gets a dictionary by which any active operation can be accessed by its RootId.
    /// </summary>
    public IReadOnlyDictionary<int, RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> ActiveById=> _activeOperations;
    
    /// <summary>
    /// Gets a collection of all of the special operations in this route
    /// </summary>
    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> Special => _special;
    
    /// <summary>
    /// Gets a collection of all of the standard operations in this route in order of their Op number
    /// </summary>
    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> Standard => _standard;

    /// <summary>
    /// Gets a collection of the default, non-archived operations which would be added to the route of a new unit
    /// being created.
    /// </summary>
    public IReadOnlyCollection<TRouteOperation> DefaultOperations => _default;

    /// <summary>
    /// Retrieves a collection of any corrective operations associated with the RootId of another operation, ordered by
    /// their sub operation number
    /// </summary>
    /// <param name="rootId"></param>
    /// <returns></returns>
    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>> Corrective(int rootId)
    {
        return _activeOperations.Values.Where(o =>
            o.Op.AddBehavior is RouteOpAdd.Corrective && o.Op.CorrectiveId == rootId)
            .OrderBy(o => o.Op.OpNumber)
            .ToArray();
    }

    /// <summary>
    /// Returns a collection of all standard operations and their corrective operations in order. Typically used for
    /// display.
    /// </summary>
    /// <returns></returns>
    public IReadOnlyCollection<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>>
        OrderedWithCorrective()
    {
        var result = new List<RouteOpAndStates<TProductType, TUnitState, TRouteOperation>>();
        foreach (var op in _standard)
        {
            result.Add(op);
            result.AddRange(Corrective(op.Op.RootId));
        }

        return result;
    }
    
}