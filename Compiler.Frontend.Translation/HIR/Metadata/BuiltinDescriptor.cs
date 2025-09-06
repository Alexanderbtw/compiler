namespace Compiler.Frontend.Translation.HIR.Metadata;

public sealed record BuiltinDescriptor(
    string Name,
    int MinArity,
    int? MaxArity, // null => no upper bound
    BuiltinAttr Attributes,
    SimpleType ReturnType = SimpleType.Unknown,
    SimpleType[]? ParamTypes = null, // may remain null until typing stage
    BuiltinLoweringKind Lowering = BuiltinLoweringKind.CallRuntime);
