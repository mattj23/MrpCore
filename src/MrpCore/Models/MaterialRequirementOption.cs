using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class MaterialRequirementOption
{
    [Key]
    public int Id { get; set; }
    
    public int MaterialRequirementId { get; set; }
    
    [ForeignKey(nameof(MaterialRequirementId))]
    public MaterialRequirement? Requirement { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the product type or stock item which is consumed by this operation
    /// </summary>
    public int TypeId { get; set; }
    
    /// <summary>
    /// Gets or sets a flag specifying if this requirement option is for a stock item (true) or for a product type
    /// </summary>
    public bool IsStockItem { get; set; }
}