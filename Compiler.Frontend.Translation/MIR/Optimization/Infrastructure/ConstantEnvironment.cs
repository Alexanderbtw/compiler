using Compiler.Frontend.Translation.MIR.Operands;

namespace Compiler.Frontend.Translation.MIR.Optimization.Infrastructure;

public sealed class ConstantEnvironment
{
    private readonly Dictionary<int, ConstantValueState> _values;

    public ConstantEnvironment()
    {
        _values = [];
    }

    private ConstantEnvironment(
        Dictionary<int, ConstantValueState> values)
    {
        _values = values;
    }

    public IReadOnlyDictionary<int, ConstantValueState> Values => _values;

    public static ConstantEnvironment Merge(
        IEnumerable<ConstantEnvironment> environments)
    {
        ConstantEnvironment[] items = environments.ToArray();

        if (items.Length == 0)
        {
            return new ConstantEnvironment();
        }

        var merged = new ConstantEnvironment();
        HashSet<int> allRegisters = [];

        foreach (ConstantEnvironment environment in items)
        {
            allRegisters.UnionWith(environment._values.Keys);
        }

        foreach (int registerId in allRegisters)
        {
            ConstantValueState current = items[0]
                .Get(registerId);

            for (var i = 1; i < items.Length; i++)
            {
                current = Meet(
                    left: current,
                    right: items[i]
                        .Get(registerId));
            }

            merged.Set(
                registerId: registerId,
                state: current);
        }

        return merged;
    }

    public ConstantEnvironment Clone()
    {
        return new ConstantEnvironment(new Dictionary<int, ConstantValueState>(_values));
    }

    public bool ContentEquals(
        ConstantEnvironment other)
    {
        if (_values.Count != other._values.Count)
        {
            return false;
        }

        foreach ((int registerId, ConstantValueState state) in _values)
        {
            if (!other._values.TryGetValue(
                    key: registerId,
                    value: out ConstantValueState otherState) || otherState != state)
            {
                return false;
            }
        }

        return true;
    }

    public ConstantValueState Get(
        int registerId)
    {
        return _values.TryGetValue(
            key: registerId,
            value: out ConstantValueState state)
            ? state
            : ConstantValueState.Unknown;
    }

    public ConstantValueState Get(
        VReg register)
    {
        return Get(register.Id);
    }

    public void Set(
        int registerId,
        ConstantValueState state)
    {
        if (state.Kind == ConstantValueKind.Unknown)
        {
            _values.Remove(registerId);

            return;
        }

        _values[registerId] = state;
    }

    public void Set(
        VReg register,
        ConstantValueState state)
    {
        Set(
            registerId: register.Id,
            state: state);
    }

    private static ConstantValueState Meet(
        ConstantValueState left,
        ConstantValueState right)
    {
        if (left.Kind == ConstantValueKind.Unknown)
        {
            return right;
        }

        if (right.Kind == ConstantValueKind.Unknown)
        {
            return left;
        }

        if (left.Kind == ConstantValueKind.Overdefined || right.Kind == ConstantValueKind.Overdefined)
        {
            return ConstantValueState.Overdefined;
        }

        return Equals(
            objA: left.Value,
            objB: right.Value)
            ? left
            : ConstantValueState.Overdefined;
    }
}
