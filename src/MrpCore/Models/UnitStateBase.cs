using System.ComponentModel.DataAnnotations;

namespace MrpCore.Models;

public class UnitStateBase : IEquatable<UnitStateBase>
{
    [Key]
    public int Id { get; set; }
    
    [Required]
    [MaxLength(32)]
    public string Name { get; set; } = null!;
    
    [MaxLength(128)]
    public string? Description { get; set; }
    
    public bool BlocksCompletion { get; set; }
    
    public bool TerminatesRoute { get; set; }

    public bool Equals(UnitStateBase? other)
    {
        if (ReferenceEquals(null, other)) return false;
        if (ReferenceEquals(this, other)) return true;
        return Id == other.Id;
    }

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(null, obj)) return false;
        if (ReferenceEquals(this, obj)) return true;
        if (obj.GetType() != this.GetType()) return false;
        return Equals((UnitStateBase)obj);
    }

    public override int GetHashCode()
    {
        return Id;
    }
}