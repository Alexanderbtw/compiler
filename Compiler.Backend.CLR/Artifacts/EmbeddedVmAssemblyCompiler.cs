using System.Text;

using Compiler.Core.Operations;
using Compiler.Backend.VM;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Runtime.VM.Bytecode;

namespace Compiler.Backend.CLR.Artifacts;

/// <summary>
///     Persists a runnable CLR assembly that embeds the VM/runtime implementation into the output project.
/// </summary>
public sealed class EmbeddedVmAssemblyCompiler : IEmbeddedVmAssemblyCompiler
{
    private static readonly string[] EmbeddedVmSourcePaths =
    [
        "Compiler.Core/Builtins/BuiltinAttr.cs",
        "Compiler.Core/Builtins/BuiltinCatalog.cs",
        "Compiler.Core/Builtins/BuiltinsCore.cs",
        "Compiler.Core/Builtins/BuiltinSignature.cs",
        "Compiler.Core/Operations/MBinOp.cs",
        "Compiler.Core/Operations/MUnOp.cs",
        "Compiler.Runtime.VM/VmValueKind.cs",
        "Compiler.Runtime.VM/VmValue.cs",
        "Compiler.Runtime.VM/VirtualMachine.cs",
        "Compiler.Runtime.VM/Bytecode/VmConstant.cs",
        "Compiler.Runtime.VM/Bytecode/VmFunction.cs",
        "Compiler.Runtime.VM/Bytecode/VmInstruction.cs",
        "Compiler.Runtime.VM/Bytecode/VmOperand.cs",
        "Compiler.Runtime.VM/Bytecode/VmProgram.cs",
        "Compiler.Runtime.VM/Execution/IVmExecutionRuntime.cs",
        "Compiler.Runtime.VM/Execution/IVmExecutionObserver.cs",
        "Compiler.Runtime.VM/Execution/HeapObject.cs",
        "Compiler.Runtime.VM/Execution/HeapObjectKind.cs",
        "Compiler.Runtime.VM/Execution/VmBuiltins.cs",
        "Compiler.Runtime.VM/Execution/VmValueOps.cs",
        "Compiler.Runtime.VM/Execution/GC/GcHeap.cs",
        "Compiler.Runtime.VM/Execution/GC/GcStats.cs",
        "Compiler.Runtime.VM/Execution/Diagnostics/VmRuntimeInstrumentation.cs",
        "Compiler.Runtime.VM/Options/GcOptions.cs"
    ];

    /// <inheritdoc />
    public GeneratedClrArtifact Compile(
        MirModule mir,
        EmbeddedVmArtifactOptions options)
    {
        ArgumentNullException.ThrowIfNull(mir);
        ArgumentNullException.ThrowIfNull(options);
        GeneratedArtifactBuilder.ValidateEntryFunction(
            entryFunctionName: options.EntryFunctionName,
            availableFunctions: mir.Functions.Select(function => function.Name));

        VmProgram program = new MirBackendCompiler().Compile(mir)
            .Program;

        var files = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [$"{options.AssemblyName}.csproj"] = BuildProjectFile(
                assemblyName: options.AssemblyName,
                targetFramework: options.TargetFramework),
            ["Program.cs"] = BuildEntryPoint(
                entryFunctionName: options.EntryFunctionName),
            ["Generated/GeneratedVmProgram.cs"] = BuildGeneratedVmProgram(program)
        };

        foreach (string sourcePath in EmbeddedVmSourcePaths)
        {
            string absolutePath = Path.Combine(
                GeneratedArtifactBuilder.FindRepositoryRoot(),
                sourcePath);

            files[Path.Combine("LinkedSources", sourcePath)] = File.ReadAllText(absolutePath);
        }

        return GeneratedArtifactBuilder.Build(
            options: options,
            files: files);
    }

    private static string BuildConstant(
        VmConstant constant)
    {
        return constant.Kind switch
        {
            VmConstantKind.Null => "VmConstant.Null()",
            VmConstantKind.I64 => $"VmConstant.FromLong({constant.Payload}L)",
            VmConstantKind.Bool => constant.Payload != 0
                ? "VmConstant.FromBool(true)"
                : "VmConstant.FromBool(false)",
            VmConstantKind.Char => $"VmConstant.FromChar('{EscapeChar((char)constant.Payload)}')",
            VmConstantKind.String => $"VmConstant.FromString(\"{GeneratedArtifactBuilder.EscapeString(constant.Text ?? string.Empty)}\")",
            _ => throw new ArgumentOutOfRangeException(nameof(constant))
        };
    }

    private static string BuildEntryPoint(
        string entryFunctionName)
    {
        return $$"""
                 using Compiler.Runtime.VM;
                 
                 var vm = new VirtualMachine();
                 vm.Execute(
                     program: GeneratedVmProgram.Create(),
                     entryFunctionName: "{{GeneratedArtifactBuilder.EscapeString(entryFunctionName)}}");
                 """;
    }

    private static string BuildGeneratedVmProgram(
        VmProgram program)
    {
        var builder = new StringBuilder();
        builder.AppendLine("using System;");
        builder.AppendLine("using System.Collections.Generic;");
        builder.AppendLine("using Compiler.Core.Operations;");
        builder.AppendLine("using Compiler.Runtime.VM.Bytecode;");
        builder.AppendLine();
        builder.AppendLine("internal static class GeneratedVmProgram");
        builder.AppendLine("{");
        builder.AppendLine("    public static VmProgram Create()");
        builder.AppendLine("    {");
        builder.AppendLine("        return new VmProgram(new Dictionary<string, VmFunction>(StringComparer.Ordinal)");
        builder.AppendLine("        {");

        foreach ((string functionName, VmFunction function) in program.Functions)
        {
            builder.AppendLine($"""            ["{GeneratedArtifactBuilder.EscapeString(functionName)}"] = {BuildFunction(function)},""");
        }

        builder.AppendLine("        });");
        builder.AppendLine("    }");
        builder.AppendLine();
        builder.AppendLine("    private static VmFunction BuildFunction(string name, int registerCount, int parameterCount, int[] parameterRegisters, VmInstruction[] instructions, VmConstant[] constants)");
        builder.AppendLine("    {");
        builder.AppendLine("        return new VmFunction(name, registerCount, parameterCount, parameterRegisters, instructions, constants);");
        builder.AppendLine("    }");
        builder.AppendLine("}");

        return builder.ToString();
    }

    private static string BuildFunction(
        VmFunction function)
    {
        string parameterRegisters = string.Join(
            ", ",
            function.ParameterRegisters);
        string instructions = string.Join(
            "," + Environment.NewLine + "                    ",
            function.Instructions.Select(BuildInstruction));
        string constants = string.Join(
            ", ",
            function.Constants.Select(BuildConstant));

        return $$"""
                 BuildFunction(
                     name: "{{GeneratedArtifactBuilder.EscapeString(function.Name)}}",
                     registerCount: {{function.RegisterCount}},
                     parameterCount: {{function.ParameterCount}},
                     parameterRegisters: [{{parameterRegisters}}],
                     instructions:
                     [
                         {{instructions}}
                     ],
                     constants:
                     [
                         {{constants}}
                     ])
                 """;
    }

    private static string BuildInstruction(
        VmInstruction instruction)
    {
        return instruction switch
        {
            VmMoveInstruction move => $"new VmMoveInstruction({move.DestinationRegister}, {BuildOperand(move.Source)})",
            VmBinaryInstruction binary => $"new VmBinaryInstruction({binary.DestinationRegister}, MBinOp.{binary.Operation}, {BuildOperand(binary.Left)}, {BuildOperand(binary.Right)})",
            VmUnaryInstruction unary => $"new VmUnaryInstruction({unary.DestinationRegister}, MUnOp.{unary.Operation}, {BuildOperand(unary.Operand)})",
            VmLoadIndexInstruction loadIndex => $"new VmLoadIndexInstruction({loadIndex.DestinationRegister}, {BuildOperand(loadIndex.ArrayOperand)}, {BuildOperand(loadIndex.IndexOperand)})",
            VmStoreIndexInstruction storeIndex => $"new VmStoreIndexInstruction({BuildOperand(storeIndex.ArrayOperand)}, {BuildOperand(storeIndex.IndexOperand)}, {BuildOperand(storeIndex.ValueOperand)})",
            VmCallInstruction call => $"new VmCallInstruction({(call.DestinationRegister is { } destinationRegister ? destinationRegister.ToString() : "null")}, \"{GeneratedArtifactBuilder.EscapeString(call.Callee)}\", [{string.Join(", ", call.Arguments.Select(BuildOperand))}])",
            VmBranchInstruction branch => $"new VmBranchInstruction({branch.TargetInstruction})",
            VmBranchConditionInstruction condition => $"new VmBranchConditionInstruction({BuildOperand(condition.Condition)}, {condition.TrueTarget}, {condition.FalseTarget})",
            VmReturnInstruction ret => $"new VmReturnInstruction({(ret.Value is { } operand ? BuildOperand(operand) : "null")})",
            _ => throw new NotSupportedException(instruction.GetType().Name)
        };
    }

    private static string BuildOperand(
        VmOperand operand)
    {
        return operand.Kind switch
        {
            VmOperandKind.Register => $"VmOperand.Register({operand.Index})",
            VmOperandKind.Constant => $"VmOperand.Constant({operand.Index})",
            _ => throw new ArgumentOutOfRangeException(nameof(operand))
        };
    }

    private static string BuildProjectFile(
        string assemblyName,
        string targetFramework)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<Project Sdk="Microsoft.NET.Sdk">""");
        builder.AppendLine("""  <PropertyGroup>""");
        builder.AppendLine("""    <OutputType>Exe</OutputType>""");
        builder.AppendLine($"""    <AssemblyName>{assemblyName}</AssemblyName>""");
        builder.AppendLine($"""    <TargetFramework>{targetFramework}</TargetFramework>""");
        builder.AppendLine("""    <Nullable>enable</Nullable>""");
        builder.AppendLine("""    <ImplicitUsings>enable</ImplicitUsings>""");
        builder.AppendLine("""    <GenerateProgramFile>false</GenerateProgramFile>""");
        builder.AppendLine("""    <EnableDefaultCompileItems>false</EnableDefaultCompileItems>""");
        builder.AppendLine("""  </PropertyGroup>""");
        builder.AppendLine("""  <ItemGroup>""");
        builder.AppendLine("""    <Compile Include="Program.cs" />""");
        builder.AppendLine("""    <Compile Include="Generated/*.cs" />""");
        builder.AppendLine("""    <Compile Include="LinkedSources/**/*.cs" />""");
        builder.AppendLine("""  </ItemGroup>""");
        builder.AppendLine("""</Project>""");

        return builder.ToString();
    }

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
