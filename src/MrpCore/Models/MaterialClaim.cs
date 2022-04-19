using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class MaterialClaim
{
    [Key]
    public int Id { get; set; }
    
    public int MaterialRequirementId { get; set; }
    
    /// <summary>
    /// Gets or sets the product unit ID associated with this claim.  If the value is zero then this claim is for a
    /// stock item.
    /// </summary>
    public int ProductUnitId { get; set; }
    
    /// <summary>
    /// Gets or sets the stock unit ID associated with this claim. IF the value is zero then the product unit ID should
    /// be non-zero.
    /// </summary>
    public int StockUnitId { get; set; }
    
    /// <summary>
    /// Gets or sets the ID of the operation result for which this claim was made.
    /// </summary>
    public int ResultId { get; set; }
    
    public double Quantity { get; set; }
}