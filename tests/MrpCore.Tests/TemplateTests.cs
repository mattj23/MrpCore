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

}