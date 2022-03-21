using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class ProductTypeBase
{
    [Key]
    public int Id { get; set; }
    
    [MaxLength(128)]
    [Required]
    public string Name { get; set; } = null!;
    
    [MaxLength(256)]
    public string? Description { get; set; }

    /// <summary>
    /// Gets or sets the quantity a unit receives upon completion. If the value is null, the quantity must be entered
    /// rather than set automatically.
    /// </summary>
    public double? UnitQuantity { get; set; }
    
    /// <summary>
    /// Gets or sets the minimum quantity that makes sense for the product. For instance, for a physical item, no
    /// quantity less than 1 makes sense.
    /// </summary>
    // public double MinQuantity { get; set; }
    
    /// <summary>
    /// Gets or sets an optional string which describes the "units" which the quantity refers to. This is just for
    /// human clarity.  Examples might be "item", "liters", "cans", etc
    /// </summary>
    public string? QuantityText { get; set; }
    
    /// <summary>
    /// Gets or sets a flag which indicates whether the item is an input material for use in other operations. This
    /// is only used to filter material input options.
    /// </summary>
    public bool Consumable { get; set; }
    
    public int? NamespaceId { get; set; }
}