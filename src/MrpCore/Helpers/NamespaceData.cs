using MrpCore.Models;

namespace MrpCore.Helpers;

public class NamespaceData<TUnitState, TProductType, TToolType> 
    where TUnitState : UnitStateBase
    where TProductType : ProductTypeBase
    where TToolType : ToolTypeBase
{
    public NamespaceData(IReadOnlyCollection<TProductType> productTypes, IReadOnlyCollection<TUnitState> states, IReadOnlyCollection<TToolType> toolTypes)
    {
        ProductTypes = productTypes;
        States = states;
        ToolTypes = toolTypes;
    }

    public IReadOnlyCollection<TProductType> ProductTypes { get; }
    public IReadOnlyCollection<TUnitState> States { get; }
    public IReadOnlyCollection<TToolType> ToolTypes { get; }
}