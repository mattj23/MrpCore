using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;
using MrpCore.Models;

namespace MrpCore.Tests.Types;

/* This file demonstrates a functioning inheritance structure which is usable with the EFCore database context and
 * the other MRP tools.
 *
 * Each custom class inherits from the base type and adds something new to it, modeling how additional concepts and
 * data can be added to the core model without losing the functionality built into the base classes.  Also added is
 * an Operator class, which is also added to the database.
 *
 * MrpCore is intended to be a data model and tools for a MRP system's *core* functionality, namely tracing product
 * items through a production route. Anything which is outside of that core feature set should be added through
 * extending the base classes. For instance, there are many ways to store and handle the concept of an operator, which
 * may make sense in some MRP systems and not in others. MrpCore's philosophy is to *not* implement this directly, but
 * to leave it easy to add without breaking core features.
 */
public class MyUnitState : UnitStateBase
{
    [Required] public int Severity { get; set; }
}

public class MyProductType : ProductTypeBase
{
    [MaxLength(12)] public string? Sku { get; set; }
}

public class MyProductUnit : ProductUnitBase<MyProductType>
{
    [MaxLength(32)] [Required] public string Serial { get; set; } = null!;
}

public class MyRouteOp : RouteOperationBase<MyProductType, MyUnitState>
{
    public string? WorkInstructionUrl { get; set; }
}

public class MyUnitOp : UnitOperationBase<MyProductUnit, MyRouteOp, MyProductType, MyUnitState>
{
    
}

public class MyOpResult : OperationResultBase<MyUnitOp, MyProductUnit, MyRouteOp, MyProductType, MyUnitState>
{
    public int OperatorId { get; set; }
    [ForeignKey(nameof(OperatorId))] public Operator Operator { get; set; }
}

public class Operator
{
    [Key] public int Id { get; set; }
    [MaxLength(64)] [Required] public string Name { get; set; } = null!;
}

public class MyContext : MrpContext<MyUnitOp, MyProductUnit, MyRouteOp, MyProductType, MyUnitState, MyOpResult>
{
    public DbSet<Operator> Operators { get; set; } = null!;
    
    public MyContext(DbContextOptions options) : base(options) {}
    
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<MyProductUnit>().HasIndex(p => p.Serial).IsUnique();
    }
}