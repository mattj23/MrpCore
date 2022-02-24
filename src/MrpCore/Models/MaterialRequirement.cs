using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class MaterialRequirement
{
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets the id of the route operation which has this requirement
    /// </summary>
    public int RouteOperationId { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the product type which is consumed by this operation
    /// </summary>
    public int ProductTypeId { get; set; }
    
    /// <summary>
    /// Gets or sets an optional quantity which specifies how many units of the product type ID will be
    /// consumed by this operation. If the quantity is null it is the same as 0 quantity consumed.
    /// </summary>
    public double? Quantity { get; set; }
    
    /// <summary>
    /// Gets or sets a flag which determines if the material is consumed even if the operation is not successful
    /// </summary>
    public bool ConsumedOnFailure { get; set; }
}