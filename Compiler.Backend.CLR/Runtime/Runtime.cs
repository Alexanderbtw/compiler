namespace Compiler.Backend.CLR.Runtime;

public static class Runtime
{
    // арифметика (возвращаем boxed long)
    public static object Add(
        object? a,
        object? b)
    {
        return L(a) + L(b);
    }

    public static object Div(
        object? a,
        object? b)
    {
        return L(a) / L(b);
    }

    public static object Eq(
        object? a,
        object? b)
    {
        return Equals(
            objA: a,
            objB: b);
    }

    public static object Ge(
        object? a,
        object? b)
    {
        return L(a) >= L(b);
    }

    public static object Gt(
        object? a,
        object? b)
    {
        return L(a) > L(b);
    }

    public static object Le(
        object? a,
        object? b)
    {
        return L(a) <= L(b);
    }

    // индексаторы для object?[] (как в твоём интерпретаторе)
    public static object? LoadIndex(
        object? arr,
        object? idx)
    {
        if (arr is not object?[] a)
        {
            throw new InvalidOperationException("indexing a non-array value");
        }

        var i = checked((int)L(idx));

        if ((uint)i >= (uint)a.Length)
        {
            throw new IndexOutOfRangeException();
        }

        return a[i];
    }

    // сравнения (boxed bool)
    public static object Lt(
        object? a,
        object? b)
    {
        return L(a) < L(b);
    }

    public static object Mod(
        object? a,
        object? b)
    {
        return L(a) % L(b);
    }

    public static object Mul(
        object? a,
        object? b)
    {
        return L(a) * L(b);
    }

    public static object Ne(
        object? a,
        object? b)
    {
        return !Equals(
            objA: a,
            objB: b);
    }

    public static object Neg(
        object? a)
    {
        return -L(a);
    }

    public static object Not(
        object? a)
    {
        return !ToBool(a);
    }

    public static object Plus(
        object? a)
    {
        return L(a);
    }

    public static void StoreIndex(
        object? arr,
        object? idx,
        object? val)
    {
        if (arr is not object?[] a)
        {
            throw new InvalidOperationException("indexing a non-array value");
        }

        var i = checked((int)L(idx));

        if ((uint)i >= (uint)a.Length)
        {
            throw new IndexOutOfRangeException();
        }

        a[i] = val;
    }
    public static object Sub(
        object? a,
        object? b)
    {
        return L(a) - L(b);
    }

    public static bool ToBool(
        object? x)
    {
        return x switch
        {
            bool b => b,
            long n => n != 0,
            null => false,
            _ => true
        };
    }
    private static long L(
        object? x)
    {
        return x switch
        {
            long n => n,
            bool b => b
                ? 1L
                : 0L,
            char c => c,
            null => throw new InvalidOperationException("null used where integer expected"),
            _ => throw new InvalidOperationException($"cannot use {x.GetType().Name} as integer")
        };
    }
}
