using MrpCore.Models;

namespace MrpCore.Helpers;

public class NamespaceData<TUnitState, TProductType> 
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
{
    public NamespaceData(IReadOnlyCollection<TProductType> productTypes, IReadOnlyCollection<TUnitState> states, IReadOnlyCollection<ToolType> toolTypes)
    {
        ProductTypes = productTypes;
        States = states;
        ToolTypes = toolTypes;
    }

    public IReadOnlyCollection<TProductType> ProductTypes { get; }
    public IReadOnlyCollection<TUnitState> States { get; }
    public IReadOnlyCollection<ToolType> ToolTypes { get; }
}