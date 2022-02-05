using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public enum RouteOpOnFailure
{
    Retry,
    Corrective,
    Special
}

public enum RouteOpAdd
{
    Default,
    NotDefault,
    Corrective,
    Special
}

public class RouteOperationBase<TProductType, TUnitState> 
    where TProductType : ProductTypeBase
    where TUnitState : UnitStateBase
{
    [Key] public int Id { get; set; }
    
    [Required] public int ProductTypeId { get; set; }
    
    [ForeignKey(nameof(ProductTypeId))] public TProductType? Type { get; set; }
    
    [Required] public int OpNumber { get; set; }
    
    public int SpecialNumber { get; set; }
    
    public RouteOpAdd AddBehavior { get; set; } 
    
    public RouteOpOnFailure FailureBehavior { get; set; }
    
    [Required] [MaxLength(128)] public string Description { get; set; } = null!;
    public ICollection<TUnitState>? Adds { get; set; }
    public ICollection<TUnitState>? Removes { get; set; }
}