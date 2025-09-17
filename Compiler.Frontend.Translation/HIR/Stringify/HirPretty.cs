using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;

namespace Compiler.Frontend.Translation.HIR.Stringify;

/// <summary>
///     Utilities to pretty-print HIR nodes and tokens.
///     Internal on purpose; used by logs and debugging helpers.
/// </summary>
internal static class HirPretty
{
    public static string Join<T>(
        IEnumerable<T> xs,
        string sep = ", ")
    {
        return string.Join(
            separator: sep,
            values: xs);
    }
    public static string Op(
        BinOp op)
    {
        return op switch
        {
            BinOp.Assign => "=",
            BinOp.Add => "+",
            BinOp.Sub => "-",
            BinOp.Mul => "*",
            BinOp.Div => "/",
            BinOp.Mod => "%",
            BinOp.Lt => "<",
            BinOp.Le => "<=",
            BinOp.Gt => ">",
            BinOp.Ge => ">=",
            BinOp.Eq => "==",
            BinOp.Ne => "!=",
            BinOp.And => "&&",
            BinOp.Or => "||",
            _ => op.ToString()
        };
    }

    public static string Op(
        UnOp op)
    {
        return op switch
        {
            UnOp.Neg => "-",
            UnOp.Not => "!",
            UnOp.Plus => "+",
            _ => op.ToString()
        };
    }

    public static string Q(
        string s)
    {
        return s
            .Replace(
                oldValue: "\\",
                newValue: "\\\\")
            .Replace(
                oldValue: "\"",
                newValue: "\\\"");
    }

    public static string Qc(
        char c)
    {
        return c switch
        {
            '\\' => "\\\\",
            '\'' => "\\'",
            '"' => "\\\"",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => c.ToString()
        };
    }
}
