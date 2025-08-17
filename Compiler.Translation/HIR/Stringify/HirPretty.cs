using System.Collections.Generic;

using Compiler.Translation.HIR.Expressions.Abstractions;

namespace Compiler.Translation.HIR.Stringify;

internal static class HirPretty
{
    public static string Op(BinOp op) => op switch
    {
        BinOp.Assign => "=",
        BinOp.Add => "+",
        BinOp.Sub => "-",
        BinOp.Mul => "*",
        BinOp.Div => "/",
        BinOp.Mod => "%",
        BinOp.Lt  => "<",
        BinOp.Le  => "<=",
        BinOp.Gt  => ">",
        BinOp.Ge  => ">=",
        BinOp.Eq  => "==",
        BinOp.Ne  => "!=",
        BinOp.And => "&&",
        BinOp.Or  => "||",
        _ => op.ToString()
    };

    public static string Op(UnOp op) => op switch
    {
        UnOp.Neg => "-",
        UnOp.Not => "!",
        UnOp.Plus => "+",
        _ => op.ToString()
    };

    public static string Q(string s)
        => s.Replace("\\", "\\\\").Replace("\"", "\\\"");

    public static string Qc(char c) => c switch
    {
        '\\' => "\\\\",
        '\'' => "\\'",
        '"' => "\\\"",
        '\n' => "\\n",
        '\r' => "\\r",
        '\t' => "\\t",
        _ => c.ToString()
    };

    public static string Join<T>(IEnumerable<T> xs, string sep = ", ")
        => string.Join(sep, xs);
}