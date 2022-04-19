using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

/// <summary>
/// A StockItem is a placeholder for common parts used in material requirements which aren't tracked in inventory but
/// rather just assumed to be adequately stocked. They exist both for BOM generation and to serve as a mechanism for
/// product variation.
/// </summary>
public class StockItem
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(128)]
    [Required]
    public string Name { get; set; } = null!;
    
    [MaxLength(256)]
    public string? Description { get; set; }
    
    [MaxLength(512)]
    public string? LinkUrl { get; set; }
    
    /// <summary>
    /// Gets or sets the minimum quantity that makes sense for the product. For instance, for a physical item, no
    /// quantity less than 1 makes sense.
    /// </summary>
    public double MinQuantity { get; set; }
    
    /// <summary>
    /// Gets or sets an optional string which describes the "units" which the quantity refers to. This is just for
    /// human clarity.  Examples might be "item", "liters", "cans", etc
    /// </summary>
    public string? QuantityText { get; set; }
    
    public int? NamespaceId { get; set; }
}