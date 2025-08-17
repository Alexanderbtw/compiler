namespace Compiler.Translation.HIR.Metadata;

[Flags]
public enum BuiltinAttr
{
    None = 0,
    Pure = 1 << 0, // не имеет побочных эффектов
    Foldable = 1 << 1, // можно сворачивать при константных аргументах
    NoThrow = 1 << 2, // не бросает исключений
    VarArgs = 1 << 3, // принимает переменное число аргументов
    Inline = 1 << 4 // совет на инлайн (если реализовано)
}