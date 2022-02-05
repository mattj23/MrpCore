using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class OperationResultBase<TUnitOperation, TProductUnit, TRouteOperation, TProductType, TUnitState>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType, TUnitState>
    where TUnitOperation : UnitOperationBase<TProductUnit, TRouteOperation, TProductType, TUnitState>
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UnitOperationId { get; set; }
    
    [ForeignKey(nameof(UnitOperationId))]
    public TUnitOperation? Operation { get; set; }
    
    public bool Pass { get; set; }
    
    public DateTime UtcTime { get; set; }
    
    [MaxLength(512)]
    public string? Notes { get; set; }
    
}