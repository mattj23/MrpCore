using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using MrpCore.Tests.Types;
using Xunit;

namespace MrpCore.Tests;

public class TemplateTests
{
    private readonly DbContextOptions<MyContext> _options;

    public TemplateTests()
    {
        _options = new DbContextOptionsBuilder<MyContext>()
            .UseInMemoryDatabase("TestDb")
            .Options;
    }

    [Fact]
    public async Task AddProductType()
    {
        var context = new MyContext(_options);
        context.Types.Add(new MyProductType { Name = "Test Product", Sku = "ABCD123" });
        await context.SaveChangesAsync();

        var result = await context.Types.FirstAsync(t => t.Name == "Test Product");
        Assert.Equal("ABCD123", result.Sku);
    }

    [Fact]
    public void AddRouteOperations()
    {
        var context = TestContext0();

        var op0 = context.RouteOperations.Include(o => o.Adds).First(o => o.OpNumber == 10);
        var op1 = context.RouteOperations.Include(o => o.Removes).First(o => o.OpNumber == 11);
        
        Assert.Contains(op0.Adds, s => s.Name == "Scrap");
        Assert.Contains(op1.Removes, s => s.Name == "Trial");
    }

    private MyContext TestContext0()
    {
        var context = new MyContext(_options);
        var productType = new MyProductType { Name = "Test Product", Sku = "1234" };
        var state0 = new MyUnitState { Name = "Scrap", BlocksCompletion = true, Severity = 0 };
        var state1 = new MyUnitState { Name = "Trial", BlocksCompletion = true, Severity = 1 };
        context.Types.Add(productType);
        context.States.Add(state0);
        context.States.Add(state1);

        var route0 = new MyRouteOp
        {
            OpNumber = 10, ProductTypeId = productType.Id, Description = "Scrap Part",
            Adds = new [] {state0}
        };

        var route1 = new MyRouteOp
        {
            OpNumber = 11, ProductTypeId = productType.Id, Description = "Remove from Trial",
            Removes = new[] { state1 }
        };

        context.RouteOperations.Add(route0);
        context.RouteOperations.Add(route1);
        context.SaveChanges();

        return context;
    }
}