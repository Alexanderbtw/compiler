using System.Text;

using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;
using Compiler.Frontend.Translation.MIR.Operands;
using Compiler.Frontend.Translation.MIR.Operands.Abstractions;

namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Persists a CLR-native assembly compiled directly from MIR without the VM runtime model.
/// </summary>
public sealed class DirectClrAssemblyCompiler : IDirectClrAssemblyCompiler
{
    /// <inheritdoc />
    public GeneratedClrArtifact Compile(
        MirModule mir,
        DirectClrArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(mir);
        ArgumentNullException.ThrowIfNull(options);
        GeneratedArtifactBuilder.ValidateEntryFunction(
            entryFunctionName: options.EntryFunctionName,
            availableFunctions: mir.Functions.Select(function => function.Name));

        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{options.AssemblyName}.csproj"] = BuildProjectFile(
                assemblyName: options.AssemblyName,
                targetFramework: options.TargetFramework),
            ["Program.cs"] = BuildProgramSource(
                mir: mir,
                entryFunctionName: options.EntryFunctionName)
        };

        return GeneratedArtifactBuilder.Build(
            options: options,
            files: files);
    }

    private static string BuildBlock(
        MirBlock block)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"    L_{GeneratedArtifactBuilder.ToSafeIdentifier(block.Name)}:");

        foreach (MirInstr instruction in block.Instructions)
        {
            builder.AppendLine("        " + BuildInstruction(instruction));
        }

        if (block.Terminator is not null)
        {
            builder.AppendLine("        " + BuildTerminator(block.Terminator));
        }

        return builder.ToString();
    }

    private static string BuildCallArgumentArray(
        IReadOnlyList<MOperand> arguments)
    {
        return $"new object?[] {{ {string.Join(", ", arguments.Select(BuildOperand))} }}";
    }

    private static string BuildFunction(
        MirFunction function)
    {
        int registerCount = GetRegisterCount(function);
        string methodName = GetMethodName(function.Name);
        var builder = new StringBuilder();

        builder.AppendLine($"    private static object? {methodName}(DirectClrRuntime runtime, object?[] args)");
        builder.AppendLine("    {");
        builder.AppendLine($"        var locals = new object?[{Math.Max(1, registerCount)}];");

        for (var index = 0; index < function.ParamRegs.Count; index++)
        {
            builder.AppendLine($"        locals[{function.ParamRegs[index].Id}] = args[{index}];");
        }

        builder.AppendLine($"        goto L_{GeneratedArtifactBuilder.ToSafeIdentifier(function.Blocks[0].Name)};");
        builder.AppendLine();

        foreach (MirBlock block in function.Blocks)
        {
            builder.Append(BuildBlock(block));
            builder.AppendLine();
        }

        builder.AppendLine("        return null;");
        builder.AppendLine("    }");

        return builder.ToString();
    }

    private static string BuildInstruction(
        MirInstr instruction)
    {
        return instruction switch
        {
            Move move => $"locals[{move.Dst.Id}] = {BuildOperand(move.Src)};",
            Bin binary => $"locals[{binary.Dst.Id}] = {BuildBinary(binary)};",
            Un unary => $"locals[{unary.Dst.Id}] = {BuildUnary(unary)};",
            LoadIndex loadIndex => $"locals[{loadIndex.Dst.Id}] = DirectClrRuntime.LoadIndex({BuildOperand(loadIndex.Arr)}, {BuildOperand(loadIndex.Index)});",
            StoreIndex storeIndex => $"DirectClrRuntime.StoreIndex({BuildOperand(storeIndex.Arr)}, {BuildOperand(storeIndex.Index)}, {BuildOperand(storeIndex.Value)});",
            Call call => BuildCall(call),
            Phi => throw new NotSupportedException("Phi nodes are not supported by direct CLR codegen."),
            Br or BrCond or Ret => throw new InvalidOperationException("Terminators must be emitted via BuildTerminator."),
            _ => throw new NotSupportedException(instruction.GetType().Name)
        };
    }

    private static string BuildTerminator(
        MirInstr instruction)
    {
        return instruction switch
        {
            Ret ret => ret.Value is null
                ? "return null;"
                : $"return {BuildOperand(ret.Value)};",
            Br branch => $"goto L_{GeneratedArtifactBuilder.ToSafeIdentifier(branch.Target.Name)};",
            BrCond branchCondition =>
                $"if (DirectClrRuntime.ToBool({BuildOperand(branchCondition.Cond)})) goto L_{GeneratedArtifactBuilder.ToSafeIdentifier(branchCondition.IfTrue.Name)}; goto L_{GeneratedArtifactBuilder.ToSafeIdentifier(branchCondition.IfFalse.Name)};",
            _ => throw new NotSupportedException(instruction.GetType().Name)
        };
    }

    private static string BuildBinary(
        Bin binary)
    {
        string left = BuildOperand(binary.L);
        string right = BuildOperand(binary.R);

        return binary.Op switch
        {
            MBinOp.Add => $"DirectClrRuntime.AsLong({left}) + DirectClrRuntime.AsLong({right})",
            MBinOp.Sub => $"DirectClrRuntime.AsLong({left}) - DirectClrRuntime.AsLong({right})",
            MBinOp.Mul => $"DirectClrRuntime.AsLong({left}) * DirectClrRuntime.AsLong({right})",
            MBinOp.Div => $"DirectClrRuntime.AsLong({left}) / DirectClrRuntime.AsLong({right})",
            MBinOp.Mod => $"DirectClrRuntime.AsLong({left}) % DirectClrRuntime.AsLong({right})",
            MBinOp.Lt => $"DirectClrRuntime.AsLong({left}) < DirectClrRuntime.AsLong({right})",
            MBinOp.Le => $"DirectClrRuntime.AsLong({left}) <= DirectClrRuntime.AsLong({right})",
            MBinOp.Gt => $"DirectClrRuntime.AsLong({left}) > DirectClrRuntime.AsLong({right})",
            MBinOp.Ge => $"DirectClrRuntime.AsLong({left}) >= DirectClrRuntime.AsLong({right})",
            MBinOp.Eq => $"DirectClrRuntime.AreEqual({left}, {right})",
            MBinOp.Ne => $"!DirectClrRuntime.AreEqual({left}, {right})",
            _ => throw new ArgumentOutOfRangeException(nameof(binary))
        };
    }

    private static string BuildCall(
        Call call)
    {
        string invocation = DirectClrBuiltins.Contains(call.Callee)
            ? $"DirectClrRuntime.InvokeBuiltin(\"{GeneratedArtifactBuilder.EscapeString(call.Callee)}\", {BuildCallArgumentArray(call.Args)})"
            : $"{GetMethodName(call.Callee)}(runtime, {BuildCallArgumentArray(call.Args)})";

        return call.Dst is { } destination
            ? $"locals[{destination.Id}] = {invocation};"
            : $"{invocation};";
    }

    private static string BuildOperand(
        MOperand operand)
    {
        return operand switch
        {
            VReg register => $"locals[{register.Id}]",
            Const constant => BuildConstant(constant.Value),
            _ => throw new NotSupportedException(operand.GetType().Name)
        };
    }

    private static string BuildProgramSource(
        MirModule mir,
        string entryFunctionName)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System.Diagnostics;");
        builder.AppendLine();
        builder.AppendLine("return GeneratedProgram.Run();");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedProgram");
        builder.AppendLine("{");
        builder.AppendLine("    public static int Run()");
        builder.AppendLine("    {");
        builder.AppendLine($"        {GetMethodName(entryFunctionName)}(DirectClrRuntime.Instance, Array.Empty<object?>());");
        builder.AppendLine("        return 0;");
        builder.AppendLine("    }");
        builder.AppendLine();

        foreach (MirFunction function in mir.Functions)
        {
            builder.AppendLine(BuildFunction(function));
        }

        builder.AppendLine("}");
        builder.AppendLine();
        builder.AppendLine(BuildRuntimeSupport());

        return builder.ToString();
    }

    private static string BuildProjectFile(
        string assemblyName,
        string targetFramework)
    {
        return $$"""
                 <Project Sdk="Microsoft.NET.Sdk">
                   <PropertyGroup>
                     <OutputType>Exe</OutputType>
                     <AssemblyName>{{assemblyName}}</AssemblyName>
                     <TargetFramework>{{targetFramework}}</TargetFramework>
                     <Nullable>enable</Nullable>
                     <ImplicitUsings>enable</ImplicitUsings>
                   </PropertyGroup>
                 </Project>
                 """;
    }

    private static string BuildRuntimeSupport()
    {
        return """
               internal sealed class DirectClrRuntime
               {
                   public static readonly DirectClrRuntime Instance = new();
               
                   public static long AsLong(object? value)
                   {
                       return value switch
                       {
                           long number => number,
                           int number => number,
                           char character => character,
                           _ => throw new InvalidOperationException($"expected integer-compatible value, got '{FormatValue(value)}'")
                       };
                   }
               
                   public static bool AreEqual(object? left, object? right)
                   {
                       if (left is long leftLong && right is char rightChar)
                       {
                           return leftLong == rightChar;
                       }
               
                       if (left is char leftCharacter && right is long rightLong)
                       {
                           return leftCharacter == rightLong;
                       }
               
                       return (left, right) switch
                       {
                           (null, null) => true,
                           (long a, long b) => a == b,
                           (bool a, bool b) => a == b,
                           (char a, char b) => a == b,
                           (string a, string b) => string.Equals(a, b, StringComparison.Ordinal),
                           (object?[] a, object?[] b) => ReferenceEquals(a, b),
                           _ => false
                       };
                   }
               
                   public static long ClockMs()
                   {
                       return (long)(Stopwatch.GetTimestamp() * 1000.0 / Stopwatch.Frequency);
                   }
               
                   public static string FormatValue(object? value)
                   {
                       return value switch
                       {
                           null => "null",
                           bool boolValue => boolValue ? "true" : "false",
                           char character => character.ToString(),
                           string text => text,
                           object?[] => "[array]",
                           _ => value.ToString() ?? "null"
                       };
                   }
               
                   public static object? InvokeBuiltin(string name, object?[] args)
                   {
                       return name switch
                       {
                           "array" => Array(args),
                           "assert" => Assert(args),
                           "chr" => Chr(args),
                           "clock_ms" => ClockMs(),
                           "len" => Len(args),
                           "ord" => Ord(args),
                           "print" => Print(args),
                           _ => throw new InvalidOperationException($"unknown builtin '{name}'")
                       };
                   }
               
                   public static object? LoadIndex(object? arrayValue, object? indexValue)
                   {
                       object?[] array = arrayValue as object?[] ?? throw new InvalidOperationException("indexing expects an array");
                       int index = checked((int)AsLong(indexValue));
               
                       if (index < 0 || index >= array.Length)
                       {
                           throw new InvalidOperationException("array index out of bounds");
                       }
               
                       return array[index];
                   }
               
                   public static void StoreIndex(object? arrayValue, object? indexValue, object? value)
                   {
                       object?[] array = arrayValue as object?[] ?? throw new InvalidOperationException("indexing expects an array");
                       int index = checked((int)AsLong(indexValue));
               
                       if (index < 0 || index >= array.Length)
                       {
                           throw new InvalidOperationException("array index out of bounds");
                       }
               
                       array[index] = value;
                   }
               
                   public static bool ToBool(object? value)
                   {
                       return value switch
                       {
                           null => false,
                           long number => number != 0,
                           int number => number != 0,
                           bool boolean => boolean,
                           char character => character != '\0',
                           string text => !string.IsNullOrEmpty(text),
                           object?[] array => array.Length != 0,
                           _ => true
                       };
                   }
               
                   private static object? Array(object?[] args)
                   {
                       if (args.Length is not (1 or 2))
                       {
                           throw new InvalidOperationException("array(n[, init]) expects 1 or 2 args");
                       }
               
                       int length = checked((int)AsLong(args[0]));
               
                       if (length < 0)
                       {
                           throw new InvalidOperationException("array length must be non-negative");
                       }
               
                       var result = new object?[length];
               
                       if (args.Length == 2)
                       {
                           System.Array.Fill(result, args[1]);
                       }
               
                       return result;
                   }
               
                   private static object? Assert(object?[] args)
                   {
                       if (args.Length is < 1 or > 2)
                       {
                           throw new InvalidOperationException("assert(cond, msg?) expects 1 or 2 args");
                       }
               
                       if (ToBool(args[0]))
                       {
                           return null;
                       }
               
                       string message = args.Length == 2
                           ? FormatValue(args[1])
                           : "assertion failed";
               
                       throw new InvalidOperationException($"assert: {message}");
                   }
               
                   private static object? Chr(object?[] args)
                   {
                       if (args.Length != 1)
                       {
                           throw new InvalidOperationException("chr(x) expects 1 arg");
                       }
               
                       long code = AsLong(args[0]);
               
                       if (code < char.MinValue || code > char.MaxValue)
                       {
                           throw new InvalidOperationException("chr(...) code point out of range");
                       }
               
                       return (char)code;
                   }
               
                   private static object? Len(object?[] args)
                   {
                       if (args.Length != 1)
                       {
                           throw new InvalidOperationException("len(x) expects 1 arg");
                       }
               
                       return args[0] switch
                       {
                           string text => (long)text.Length,
                           object?[] array => (long)array.Length,
                           _ => throw new InvalidOperationException("len(...) expects string or array")
                       };
                   }
               
                   private static object? Ord(object?[] args)
                   {
                       if (args.Length != 1)
                       {
                           throw new InvalidOperationException("ord(c) expects 1 arg");
                       }
               
                       return args[0] switch
                       {
                           char character => (long)character,
                           string { Length: 1 } text => (long)text[0],
                           _ => throw new InvalidOperationException("ord(...) expects char or 1-length string")
                       };
                   }
               
                   private static object? Print(object?[] args)
                   {
                       Console.Out.WriteLine(string.Join(" ", args.Select(FormatValue)));
                       return null;
                   }
               }
               """;
    }

    private static string BuildUnary(
        Un unary)
    {
        string operand = BuildOperand(unary.X);

        return unary.Op switch
        {
            MUnOp.Neg => $"-DirectClrRuntime.AsLong({operand})",
            MUnOp.Not => $"!DirectClrRuntime.ToBool({operand})",
            MUnOp.Plus => $"DirectClrRuntime.AsLong({operand})",
            _ => throw new ArgumentOutOfRangeException(nameof(unary))
        };
    }

    private static string BuildConstant(
        object? value)
    {
        return value switch
        {
            null => "null",
            bool boolValue => boolValue
                ? "true"
                : "false",
            char character => $"'{EscapeChar(character)}'",
            string text => $"\"{GeneratedArtifactBuilder.EscapeString(text)}\"",
            int number => $"{number}L",
            long number => $"{number}L",
            _ => throw new NotSupportedException(value.GetType().Name)
        };
    }

    private static int GetRegisterCount(
        MirFunction function)
    {
        int maxRegisterId = function.ParamRegs.Count == 0
            ? -1
            : function.ParamRegs.Max(register => register.Id);

        foreach (MirBlock block in function.Blocks)
        {
            foreach (MirInstr instruction in block.Instructions)
            {
                maxRegisterId = Math.Max(
                    maxRegisterId,
                    GetRegisterId(instruction));
            }
        }

        return maxRegisterId + 1;
    }

    private static int GetRegisterId(
        MirInstr instruction)
    {
        return instruction switch
        {
            Move move => move.Dst.Id,
            Bin binary => binary.Dst.Id,
            Un unary => unary.Dst.Id,
            LoadIndex loadIndex => loadIndex.Dst.Id,
            Call { Dst: { } destination } => destination.Id,
            _ => -1
        };
    }

    private static string GetMethodName(
        string functionName)
    {
        return $"Fn_{GeneratedArtifactBuilder.ToSafeIdentifier(functionName)}";
    }

    private static readonly HashSet<string> DirectClrBuiltins =
    [
        "array",
        "assert",
        "chr",
        "clock_ms",
        "len",
        "ord",
        "print"
    ];

    private static string EscapeChar(
        char value)
    {
        return value switch
        {
            '\'' => "\\'",
            '\\' => "\\\\",
            '\0' => "\\0",
            '\n' => "\\n",
            '\r' => "\\r",
            '\t' => "\\t",
            _ => value.ToString()
        };
    }
}
