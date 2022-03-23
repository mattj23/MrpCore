using MrpCore.Models;

namespace MrpCore.Helpers;

/// <summary>
/// Represents a "master" manufacturing route for a specific type of product.
/// 
/// It is constructed from the complete collection of all route operations and computes from it meaning inherent in
/// the complete set, such as operation order, relations between corrective operations and their parent, and special
/// route operations.
/// </summary>
/// <typeparam name="TProductType"></typeparam>
/// <typeparam name="TUnitState"></typeparam>
/// <typeparam name="TRouteOperation"></typeparam>
/// <typeparam name="TToolRequirement"></typeparam>
public class Route<TProductType, TUnitState, TRouteOperation, TToolRequirement>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TRouteOperation : RouteOperationBase<TProductType>
    where TToolRequirement : ToolRequirementBase
{
    private readonly Dictionary<int, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> _allOperations;
    private readonly Dictionary<int, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> _activeOperations;
    private readonly RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>[] _special;
    private readonly RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>[] _standard;
    private readonly TRouteOperation[] _default;

    public Route(int productTypeId, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>[] allOperations)
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
        _activeOperations = new Dictionary<int, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>>();
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
    public IReadOnlyDictionary<int, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> AllById=> _allOperations;
    
    /// <summary>
    /// Gets a dictionary by which any active operation can be accessed by its RootId.
    /// </summary>
    public IReadOnlyDictionary<int, RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> ActiveById=> _activeOperations;
    
    /// <summary>
    /// Gets a collection of all of the special operations in this route
    /// </summary>
    public IReadOnlyCollection<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> Special => _special;
    
    /// <summary>
    /// Gets a collection of all of the standard operations in this route in order of their Op number
    /// </summary>
    public IReadOnlyCollection<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> Standard => _standard;

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
    public IReadOnlyCollection<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>> Corrective(int rootId)
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
    public IReadOnlyCollection<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>>
        OrderedWithCorrective()
    {
        var result = new List<RouteOpAndData<TProductType, TUnitState, TRouteOperation, TToolRequirement>>();
        foreach (var op in _standard)
        {
            result.Add(op);
            result.AddRange(Corrective(op.Op.RootId));
        }

        return result;
    }
    
}