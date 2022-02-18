using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class MaterialClaim
{
    [Key]
    public int Id { get; set; }
    
    public int ProductUnitId { get; set; }
    
    public int ResultId { get; set; }
    
    public int? QuantityConsumed { get; set; }
}