using MrpCore.Models;

namespace MrpCore.Helpers;

public class OpStateChanges<TUnitState> where TUnitState : UnitStateBase
{
    private readonly TUnitState[] _add;
    private readonly TUnitState[] _remove;

    public OpStateChanges(IEnumerable<TUnitState> add, IEnumerable<TUnitState> remove)
    {
        _add = add.ToArray();
        _remove = remove.ToArray();
    }

    public IReadOnlyCollection<TUnitState> Adds => _add;
    public IReadOnlyCollection<TUnitState> Removes => _remove;
}