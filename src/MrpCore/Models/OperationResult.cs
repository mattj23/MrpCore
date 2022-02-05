using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace MrpCore.Models;

public class OperationResult
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    public int UnitOperationId { get; set; }
    
    [ForeignKey(nameof(UnitOperationId))]
    public UnitOperation UnitOperation { get; set; }
    
    public bool Pass { get; set; }
    
    public DateTime UtcTime { get; set; }
    
    [MaxLength(512)]
    public string? Notes { get; set; }
    
}