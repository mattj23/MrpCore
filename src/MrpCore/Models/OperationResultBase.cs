using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class OperationResultBase<TProductType, TUnitState, TProductUnit, TRouteOperation, TUnitOperation>
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TProductUnit : ProductUnitBase<TProductType>
    where TRouteOperation : RouteOperationBase<TProductType>
    where TUnitOperation : UnitOperationBase<TProductType, TUnitState, TProductUnit, TRouteOperation>
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UnitOperationId { get; set; }
    
    [ForeignKey(nameof(UnitOperationId))]
    public TUnitOperation? Operation { get; set; }
    
    public bool Pass { get; set; }
    
    public DateTime UtcTime { get; set; }
    
}