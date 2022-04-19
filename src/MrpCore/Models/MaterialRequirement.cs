using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class MaterialRequirement
{
    public MaterialRequirement()
    {
        Options = new List<MaterialRequirementOption>();
    }
    
    [Key]
    public int Id { get; set; }
    
    /// <summary>
    /// Gets or sets a description explaining what this material requirement is for
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Gets or sets the id of the route operation which has this requirement
    /// </summary>
    public int RouteOpRootId { get; set; }
    
    /// <summary>
    /// Gets or sets an optional quantity which specifies how many units of the product type ID will be
    /// consumed by this operation. 
    /// </summary>
    public double Quantity { get; set; }
    
    /// <summary>
    /// Gets or sets a flag which determines if the material is consumed even if the operation is not successful
    /// </summary>
    public bool ConsumedOnFailure { get; set; }
    
    public ICollection<MaterialRequirementOption> Options { get; set; }
    
    public bool Archived { get; set; }
}