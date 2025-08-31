using System.Reflection;
using System.Text;

using Antlr4.Runtime;

using Compiler.Backend.CLR;
using Compiler.Backend.VM;
using Compiler.Frontend;
using Compiler.Frontend.Translation.HIR;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Expressions;
using Compiler.Frontend.Translation.HIR.Expressions.Abstractions;
using Compiler.Frontend.Translation.HIR.Semantic;
using Compiler.Frontend.Translation.HIR.Semantic.Exceptions;
using Compiler.Frontend.Translation.HIR.Statements;
using Compiler.Frontend.Translation.HIR.Statements.Abstractions;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Instructions;
using Compiler.Frontend.Translation.MIR.Instructions.Abstractions;

using Xunit.Abstractions;
using Xunit.Sdk;

namespace Compiler.Tests;

internal static class TestUtils
{
    public static readonly string[] SourceExtensions = [".minl"];

    // --- Константы/настройки ---
    // в TestUtils.cs (или где объявлен список дефолтных директорий)
    private static readonly string[] DefaultProgramDirs =
    [
        Path.Combine(
            path1: AppContext.BaseDirectory,
            path2: "Tasks")
    ];

    public static IEnumerable<MirInstr> AllMirInstructions(
        MirFunction f)
    {
        foreach (MirBlock b in f.Blocks)
        {
            foreach (MirInstr i in b.Instructions)
            {
                yield return i;
            }

            if (b.Terminator is not null)
            {
                yield return b.Terminator;
            }
        }
    }

    // --- Unified assertions for interpreter / backend runs ---
    public static void AssertProgramResult(
        object? expectedRet,
        string? expectedStdout,
        object? actualRet,
        string actualStdout,
        ITestOutputHelper? log = null)
    {
        if (expectedStdout is not null)
        {
            log?.WriteLine(
                format: "Expected output:\n{0}",
                expectedStdout);

            log?.WriteLine(
                format: "Actual output:\n{0}",
                actualStdout);

            Assert.Equal(
                expected: expectedStdout,
                actual: NormalizeNewlines(actualStdout)
                    .TrimEnd());
        }

        if (expectedRet is not null)
        {
            log?.WriteLine(
                format: "Expected return:\n{0}",
                expectedRet);

            log?.WriteLine(
                format: "Actual return:\n{0}",
                actualRet);

            AssertReturnEqual(
                expected: expectedRet,
                actual: actualRet);
        }
    }

    public static void AssertReturnEqual(
        object expected,
        object? actual)
    {
        actual = TryUnwrapVmValue(actual);

        switch (expected)
        {
            case long el:
                Assert.True(
                    condition: actual is long,
                    userMessage: $"Expected long but got {actual?.GetType().Name ?? "null"}");

                Assert.Equal(
                    expected: el,
                    actual: (long)actual!);

                break;

            case bool eb:
                Assert.True(
                    condition: actual is bool,
                    userMessage: $"Expected bool but got {actual?.GetType().Name ?? "null"}");

                Assert.Equal(
                    expected: eb,
                    actual: (bool)actual!);

                break;

            case string es:
                Assert.True(
                    condition: actual is string,
                    userMessage: $"Expected string but got {actual?.GetType().Name ?? "null"}");

                Assert.Equal(
                    expected: es,
                    actual: (string)actual!);

                break;

            case object?[] earr:
                {
                    object?[] arr = Assert.IsAssignableFrom<object?[]>(actual ?? Array.Empty<object>());
                    Assert.Equal(
                        expected: earr.Length,
                        actual: arr.Length);

                    for (int i = 0; i < earr.Length; i++)
                    {
                        switch (earr[i])
                        {
                            case long ln:
                                Assert.True(
                                    condition: arr[i] is long,
                                    userMessage: $"Expected {nameof(Int64)} but got {arr[i]?.GetType().Name ?? "null"}");

                                Assert.Equal(
                                    expected: ln,
                                    actual: (long)arr[i]!);

                                break;
                            case string ls:
                                Assert.True(
                                    condition: arr[i] is string,
                                    userMessage: $"Expected {nameof(String)} but got {arr[i]?.GetType().Name ?? "null"}");

                                Assert.Equal(
                                    expected: ls,
                                    actual: (string)arr[i]!);

                                break;
                            case bool lb:
                                Assert.True(
                                    condition: arr[i] is bool,
                                    userMessage: $"Expected {nameof(Boolean)} but got {arr[i]?.GetType().Name ?? "null"}");

                                Assert.Equal(
                                    expected: lb,
                                    actual: (bool)arr[i]!);

                                break;
                            default:
                                Assert.Fail($"Unsupported array element at {i}: {earr[i]?.GetType().Name}");

                                break;
                        }
                    }

                    break;
                }

            default:
                Assert.Fail($"Unsupported expected type: {expected.GetType().Name}");

                break;
        }
    }

    public static void AssertSemanticFails(
        string src,
        string expectedSubstring)
    {
        ICharStream? input = CharStreams.fromString(src);
        var lexer = new MiniLangLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        ProgramHir hir = new HirBuilder().Build(parser.program());
        var ex = Assert.Throws<SemanticException>(() => new SemanticChecker().Check(hir));
        Assert.Contains(
            expectedSubstring: expectedSubstring,
            actualString: ex.Message);
    }

    public static void AssertSemanticOk(
        string src)
    {
        ICharStream? input = CharStreams.fromString(src);
        var lexer = new MiniLangLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        ProgramHir hir = new HirBuilder().Build(parser.program());
        new SemanticChecker().Check(hir); // must not throw
    }

    public static VmModule BuildBytecode(
        string src)
    {
        MirModule mir = BuildMir(src);

        return new MirToBytecode().Lower(mir);
    }

    // --- Общий пайплайн сборки IR ---
    public static ProgramHir BuildHir(
        string src)
    {
        MiniLangParser parser = CreateParser(src);
        MiniLangParser.ProgramContext? tree = parser.program();
        ProgramHir hir = new HirBuilder().Build(tree);
        new SemanticChecker().Check(hir);

        return hir;
    }

    public static MirModule BuildMir(
        string src)
    {
        ProgramHir hir = BuildHir(src);

        return new HirToMir().Lower(hir);
    }

    // --- Поиск программ для [Theory] ---
    public static IEnumerable<object[]> EnumerateProgramFiles()
    {
        string dir = GetProgramsDir();

        return Directory
            .EnumerateFiles(
                path: dir,
                searchPattern: "*",
                searchOption: SearchOption.TopDirectoryOnly)
            .Where(f => SourceExtensions.Contains(
                value: Path.GetExtension(f),
                comparer: StringComparer.OrdinalIgnoreCase))
            .OrderBy(
                keySelector: f => f,
                comparer: StringComparer.OrdinalIgnoreCase)
            .Select(f => new object[] { f });
    }

    public static IEnumerable<ExprHir> FlattenExpr(
        ExprHir? e)
    {
        if (e is null)
        {
            yield break;
        }

        yield return e;

        switch (e)
        {
            case BinHir b:
                foreach (ExprHir s in FlattenExpr(b.Left))
                {
                    yield return s;
                }

                foreach (ExprHir s in FlattenExpr(b.Right))
                {
                    yield return s;
                }

                break;
            case UnHir u:
                foreach (ExprHir s in FlattenExpr(u.Operand))
                {
                    yield return s;
                }

                break;
            case CallHir c:
                foreach (ExprHir s in FlattenExpr(c.Callee))
                {
                    yield return s;
                }

                foreach (ExprHir a in c.Args)
                foreach (ExprHir s in FlattenExpr(a))
                {
                    yield return s;
                }

                break;
            case IndexHir ix:
                foreach (ExprHir s in FlattenExpr(ix.Target))
                {
                    yield return s;
                }

                foreach (ExprHir s in FlattenExpr(ix.Index))
                {
                    yield return s;
                }

                break;
        }
    }

    public static IEnumerable<ExprHir> FlattenStmts(
        StmtHir s)
    {
        return s switch
        {
            BlockHir b => b.Statements.SelectMany(FlattenStmts),
            LetHir v => v.Init is null
                ? []
                : FlattenExpr(v.Init),
            ExprStmtHir e => e.Expr is null
                ? []
                : FlattenExpr(e.Expr),
            IfHir i => FlattenExpr(i.Cond)
                .Concat(FlattenStmts(i.Then))
                .Concat(
                    i.Else is not null
                        ? FlattenStmts(i.Else)
                        : []),
            WhileHir w => FlattenExpr(w.Cond)
                .Concat(FlattenStmts(w.Body)),
            _ => []
        };
    }

    public static string GetProgramsDir()
    {
        string? env = Environment.GetEnvironmentVariable("MLANG_TEST_PROGRAMS");

        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env))
        {
            return Path.GetFullPath(env);
        }

        foreach (string d in DefaultProgramDirs)
        {
            if (Directory.Exists(d))
            {
                return Path.GetFullPath(d);
            }
        }

        throw new DirectoryNotFoundException("Set env MLANG_TEST_PROGRAMS or create testdata/programs relative to the test project.");
    }

    public static MirFunction LowerFunction(
        string src,
        string name = "main")
    {
        MirModule mir = BuildMir(src);

        return mir.Functions.Single(f => f.Name == name);
    }

    public static string NormalizeNewlines(
        string s)
    {
        return s.Replace(
            oldValue: "\r\n",
            newValue: "\n");
    }

    public static object? ParseValue(
        string text)
    {
        if (string.Equals(
                a: text,
                b: "null",
                comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(
                a: text,
                b: "true",
                comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (string.Equals(
                a: text,
                b: "false",
                comparisonType: StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
        {
            return text.Substring(
                startIndex: 1,
                length: text.Length - 2);
        }

        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
        {
            string inner = text
                .Substring(
                    startIndex: 1,
                    length: text.Length - 2)
                .Trim();

            if (inner.Length == 0)
            {
                return Array.Empty<object?>();
            }

            return inner
                .Split(',')
                .Select(p => ParseValue(p.Trim()))
                .ToArray();
        }

        if (long.TryParse(
                s: text,
                result: out long n))
        {
            return n;
        }

        throw new FormatException($"Unsupported RET format: '{text}'");
    }

    // --- Ожидания (RET/STDOUT) ---
    // sidecar: <file>.ret / <file>.out  (опционально)
    // inline:  // RET: <value>           // STDOUT: <text>  (или // EXPECT:)
    public static (object? expectedRet, string? expectedStdout) ReadExpectations(
        string programPath,
        string src)
    {
        string baseName = Path.Combine(
            path1: Path.GetDirectoryName(programPath)!,
            path2: Path.GetFileNameWithoutExtension(programPath)!);

        string retPath = baseName + ".ret";
        string outPath = baseName + ".out";

        object? expRet = File.Exists(retPath)
            ? ParseValue(
                File
                    .ReadAllText(retPath)
                    .Trim())
            : null;

        string? expOut = File.Exists(outPath)
            ? NormalizeNewlines(File.ReadAllText(outPath))
                .TrimEnd()
            : null;

        foreach (string line in src.Split('\n'))
        {
            string t = line.Trim();

            if (t.StartsWith(
                    value: "// RET:",
                    comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                expRet = ParseValue(
                    t[7..]
                        .Trim());
            }
            else if (t.StartsWith(
                         value: "// STDOUT:",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                expOut = t[10..]
                    .TrimEnd();
            }
            else if (t.StartsWith(
                         value: "// EXPECT:",
                         comparisonType: StringComparison.OrdinalIgnoreCase))
            {
                expOut = t[10..]
                    .TrimEnd();
            }
        }

        return (expRet, expOut);
    }

    public static void RunAndAssertFile(
        string path,
        Func<string, (object? ret, string stdout)> runner,
        ITestOutputHelper? log = null)
    {
        string src = File.ReadAllText(path);
        (object? expRet, string? expOut) = ReadExpectations(
            programPath: path,
            src: src);

        (object? ret, string stdout) = runner(src);
        AssertProgramResult(
            expectedRet: expRet,
            expectedStdout: expOut,
            actualRet: ret,
            actualStdout: stdout,
            log: log);
    }

    public static void RunAndAssertSource(
        string src,
        Func<string, (object? ret, string stdout)> runner,
        string? pathForExpectations = null,
        ITestOutputHelper? log = null)
    {
        (object? expRet, string? expOut) = pathForExpectations is null
            ? (null, null)
            : ReadExpectations(
                programPath: pathForExpectations,
                src: src);

        (object? ret, string stdout) = runner(src);
        AssertProgramResult(
            expectedRet: expRet,
            expectedStdout: expOut,
            actualRet: ret,
            actualStdout: stdout,
            log: log);
    }

    public static (object? ret, string stdout) RunCil(
        string src)
    {
        MirModule mir = BuildMir(src);
        var backend = new CilBackend();

        var sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));

        try
        {
            object? ret = backend.RunMain(mir);

            return (ret, sb
                .ToString()
                .TrimEnd());
        }
        finally { Console.SetOut(old); }
    }

    // --- Запуски: интерпретатор и CIL ---
    public static (object? ret, string stdout) RunInterpreter(
        string src)
    {
        ProgramHir hir = BuildHir(src);
        var interp = new Interpreter.Interpreter(hir);

        var sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));

        try
        {
            object? ret = interp.Run();

            return (ret, sb
                .ToString()
                .TrimEnd());
        }
        finally { Console.SetOut(old); }
    }

    public static (object? ret, string stdout) RunVm(
        string src)
    {
        VmModule bytecode = BuildBytecode(src);
        var virtualMachine = new VirtualMachine(bytecode);

        var sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));

        try
        {
            Value ret = virtualMachine.Execute();

            return (ret, sb
                .ToString()
                .TrimEnd());
        }
        finally { Console.SetOut(old); }
    }

    private static MiniLangParser CreateParser(
        string src)
    {
        var str = new AntlrInputStream(src);
        var lexer = new Frontend.MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);

        return new MiniLangParser(tokens);
    }

    private static object? TryUnwrapVmValue(
        object? v)
    {
        return v switch
        {
            null => null,
            Value value => value.Tag switch
            {
                ValueTag.Null => null,
                ValueTag.I64 => value.AsLong(),
                ValueTag.Bool => value.AsBool(),
                ValueTag.Char => value.AsChar(),
                ValueTag.String => value.AsStr(),
                ValueTag.Array => value.AsArr(),
                ValueTag.Object => value,
                _ => throw new ArgumentOutOfRangeException()
            },
            _ => v
        };
    }
}

// xUnit DataAttribute для файловых программ: [ProgramFilesData]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ProgramFilesDataAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(
        MethodInfo testMethod)
    {
        return TestUtils.EnumerateProgramFiles();
    }
}
