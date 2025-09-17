namespace Compiler.Frontend.Translation.HIR.Metadata;

/// <summary>
///     One builtin entry: carries arity constraints, attributes, return type and a lowering hint.
/// </summary>
public sealed record BuiltinDescriptor(
    string Name,
    int MinArity,
    int? MaxArity, // null => no upper bound
    BuiltinAttr Attributes,
    SimpleType ReturnType = SimpleType.Unknown,
    SimpleType[]? ParamTypes = null, // may remain null until typing stage
    BuiltinLoweringKind Lowering = BuiltinLoweringKind.CallRuntime);
