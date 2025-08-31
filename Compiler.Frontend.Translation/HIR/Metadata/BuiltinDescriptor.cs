namespace Compiler.Frontend.Translation.HIR.Metadata;

public sealed record BuiltinDescriptor(
    string Name,
    int MinArity,
    int? MaxArity, // null => нет верхнего предела
    BuiltinAttr Attributes,
    SimpleType ReturnType = SimpleType.Unknown,
    SimpleType[]? ParamTypes = null, // можно оставить null до этапа типизации
    BuiltinLoweringKind Lowering = BuiltinLoweringKind.CallRuntime);
