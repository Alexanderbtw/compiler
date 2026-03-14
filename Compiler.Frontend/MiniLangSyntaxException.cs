namespace Compiler.Frontend;

public sealed class MiniLangSyntaxException(
    string message) : Exception(message);
