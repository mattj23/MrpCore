using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class ProductUnitBase<TProductType> where TProductType : ProductTypeBase
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int ProductTypeId { get; set; }
    
    [ForeignKey(nameof(ProductTypeId))]
    public TProductType? Type { get; set; }
    
    public DateTime CreatedUtc { get; set; }
    
    /// <summary>
    /// Gets or sets a flag which determines whether the item has been archived. Archived items do not get queried
    /// during operations concerning WIP or active inventory. They still exist in the database and are queried during
    /// operations which examine historical records.
    /// </summary>
    public bool Archived { get; set; }
}