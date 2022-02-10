using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

/// <summary>
/// A "route operation" is any class which inherits from this base type, and represents a conceptual operation on a
/// product unit's "master" route.
///
/// </summary>
/// <typeparam name="TProductType"></typeparam>
public class RouteOperationBase<TProductType> 
    where TProductType : ProductTypeBase
{
    /// <summary>
    /// Gets the unique ID integer of this route operation.
    /// </summary>
    [Key] public int Id { get; set; }
    
    /// <summary>
    /// Gets the unique ID integer of the original (root) version of this route operation. When a new route operation
    /// is created this value will match the Id property. As the operation is updated, this value will remain the same
    /// while the Id property changes. The entire history of a route operation can be queried by this value.
    /// </summary>
    public int RootId { get; set; }
    
    /// <summary>
    /// Gets an integer which represents the version of the current route operation with respect to the original (root)
    /// route operation. Starts at 0 and increments every time a route operation is updated.  The combination of RootId
    /// and RootVersion must be unique within the database.
    /// </summary>
    public int RootVersion { get; set; }
    
    /// <summary>
    /// Gets the integer ID of the product type.
    /// </summary>
    [Required] public int ProductTypeId { get; set; }
    
    /// <summary>
    /// Gets the product type this route operation is associated with
    /// </summary>
    [ForeignKey(nameof(ProductTypeId))] public TProductType? Type { get; set; }
    
    /// <summary>
    /// Gets the op number of the operation. If this is a standard route operation, the op number determines the
    /// overall sequence in which the operations must be performed. If this operation is part of a corrective chain,
    /// the op number determines the sequence in which the corrective operations must be performed. For special
    /// operations this number is ignored.
    /// </summary>
    [Required] public int OpNumber { get; set; }
    
    /// <summary>
    /// Gets the RootId of the route operation to which this operation belongs to as a corrective action. If this
    /// operation is not a corrective action, this value is ignored.
    /// </summary>
    public int CorrectiveId { get; set; }
    
    /// <summary>
    /// Gets the RootId of the special route operation which is invoked if this part fails.
    /// </summary>
    public int? SpecialFailId { get; set; }

    /// <summary>
    /// Gets or sets a flag which determines if this route operation is not active.  Route operations cannot be deleted
    /// once they are referenced, so this serves the same purpose as delete.
    /// </summary>
    public bool Archived { get; set; } = false;
    
    public RouteOpAdd AddBehavior { get; set; } 
    
    public RouteOpFailure FailureBehavior { get; set; }
    
    [Required] [MaxLength(128)] public string Description { get; set; } = null!;

    /// <summary>
    /// Throws a ValidationException if there is something invalid about the operation.
    /// </summary>
    /// <exception cref="ValidationException">Thrown with a message describing any misconfiguration</exception>
    public void ThrowIfInvalid()
    {
        if (AddBehavior is RouteOpAdd.Corrective &&
            FailureBehavior is RouteOpFailure.CorrectiveProceed or RouteOpFailure.CorrectiveReturn)
        {
            throw new ValidationException("A corrective operation cannot have corrective failure behavior");
        }
    }
}

public enum RouteOpFailure
{
    /// <summary>
    /// Allows a route operation to be retried on failure
    /// </summary>
    Retry,
    
    /// <summary>
    /// Invokes a corrective chain which, on successful completion, work transitions to the next operation after the
    /// failed operation and does not return to it.
    /// </summary>
    CorrectiveProceed,
    
    /// <summary>
    /// Invokes a corrective chain which, on successful completion, returns to the failed operation so that it may be
    /// retried.
    /// </summary>
    CorrectiveReturn,
    
    /// <summary>
    /// Invokes a special operation which terminates the route. The SpecialFailId must be set to an operation which
    /// has the TerminatesRoute property set to true.
    /// </summary>
    TerminateWithSpecial
}

public enum RouteOpAdd
{
    /// <summary>
    /// A default route op is added to every new product unit's route on its creation
    /// </summary>
    Default,
    
    /// <summary>
    /// A non-default route op is not added automatically to every new product's unit route, but can be added separately
    /// by the client code/system
    /// </summary>
    NotDefault,
    
    /// <summary>
    /// A corrective operation is part of a corrective chain attached to another operation. It is only added to the
    /// route when that operation fails.
    /// </summary>
    Corrective,
    
    /// <summary>
    /// A special route operation is never added to a unit's route automatically and does not have an ordering
    /// relation with the rest of the operations. It is used to add explicit states to a unit.
    /// </summary>
    Special
}
