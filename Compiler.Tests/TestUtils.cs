using System.Reflection;
using System.Text;

using Antlr4.Runtime;

using Compiler.Backend.CLR;
using Compiler.Frontend;
using Compiler.Translation.HIR;
using Compiler.Translation.HIR.Common;
using Compiler.Translation.HIR.Expressions;
using Compiler.Translation.HIR.Expressions.Abstractions;
using Compiler.Translation.HIR.Semantic;
using Compiler.Translation.HIR.Semantic.Exceptions;
using Compiler.Translation.HIR.Statements;
using Compiler.Translation.HIR.Statements.Abstractions;
using Compiler.Translation.MIR;
using Compiler.Translation.MIR.Common;
using Compiler.Translation.MIR.Instructions.Abstractions;

using Xunit.Sdk;

namespace Compiler.Tests;

internal static class TestUtils
{
    public readonly static string[] SourceExtensions = { ".minl" };

    // --- Константы/настройки ---
    // в TestUtils.cs (или где объявлен список дефолтных директорий)
    private static readonly string[] DefaultProgramDirs =
    {
        Path.Combine(AppContext.BaseDirectory, "Tasks"),
    };

    public static void AssertReturnEqual(object expected, object? actual)
    {
        switch (expected)
        {
            case long el:
                Assert.IsType<long>(actual);
                Assert.Equal(el, (long)actual!);
                break;

            case bool eb:
                Assert.IsType<bool>(actual);
                Assert.Equal(eb, (bool)actual!);
                break;

            case string es:
                Assert.IsType<string>(actual);
                Assert.Equal(es, (string)actual!);
                break;

            case object?[] earr:
            {
                object?[] arr = Assert.IsType<object?[]>(actual);
                Assert.Equal(earr.Length, arr.Length);
                for (var i = 0; i < earr.Length; i++)
                {
                    switch (earr[i])
                    {
                        case long ln:
                            Assert.IsType<long>(arr[i]);
                            Assert.Equal(ln, (long)arr[i]!);
                            break;
                        case string ls:
                            Assert.IsType<string>(arr[i]);
                            Assert.Equal(ls, (string)arr[i]!);
                            break;
                        case bool lb:
                            Assert.IsType<bool>(arr[i]);
                            Assert.Equal(lb, (bool)arr[i]!);
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

    // --- Общий пайплайн сборки IR ---
    public static ProgramHir BuildHir(string src)
    {
        MiniLangParser parser = CreateParser(src);
        MiniLangParser.ProgramContext? tree = parser.program();
        ProgramHir hir = new HirBuilder().Build(tree);
        new SemanticChecker().Check(hir);
        return hir;
    }

    public static MirModule BuildMir(string src)
    {
        ProgramHir hir = BuildHir(src);
        return new HirToMir().Lower(hir);
    }

    // --- Поиск программ для [Theory] ---
    public static IEnumerable<object[]> EnumerateProgramFiles()
    {
        string dir = GetProgramsDir();
        return Directory.EnumerateFiles(dir, "*", SearchOption.TopDirectoryOnly)
            .Where(f => SourceExtensions.Contains(Path.GetExtension(f), StringComparer.OrdinalIgnoreCase))
            .OrderBy(f => f, StringComparer.OrdinalIgnoreCase)
            .Select(f => new object[] { f });
    }

    public static string GetProgramsDir()
    {
        string? env = Environment.GetEnvironmentVariable("MLANG_TEST_PROGRAMS");
        if (!string.IsNullOrWhiteSpace(env) && Directory.Exists(env)) return Path.GetFullPath(env);

        foreach (string d in DefaultProgramDirs)
            if (Directory.Exists(d))
                return Path.GetFullPath(d);

        throw new DirectoryNotFoundException(
            "Set env MLANG_TEST_PROGRAMS or create testdata/programs relative to the test project.");
    }

    public static string NormalizeNewlines(string s) => s.Replace("\r\n", "\n");

    public static object? ParseValue(string text)
    {
        if (string.Equals(text, "null", StringComparison.OrdinalIgnoreCase)) return null;
        if (string.Equals(text, "true", StringComparison.OrdinalIgnoreCase)) return true;
        if (string.Equals(text, "false", StringComparison.OrdinalIgnoreCase)) return false;

        if (text.Length >= 2 && text[0] == '"' && text[^1] == '"')
            return text.Substring(1, text.Length - 2);

        if (text.Length >= 2 && text[0] == '[' && text[^1] == ']')
        {
            string inner = text.Substring(1, text.Length - 2).Trim();
            if (inner.Length == 0) return Array.Empty<object?>();
            return inner.Split(',').Select(p => ParseValue(p.Trim())).ToArray();
        }

        if (long.TryParse(text, out long n)) return n;

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
            Path.GetDirectoryName(programPath)!,
            Path.GetFileNameWithoutExtension(programPath)!);
        string retPath = baseName + ".ret";
        string outPath = baseName + ".out";

        object? expRet = File.Exists(retPath) ? ParseValue(File.ReadAllText(retPath).Trim()) : null;
        string? expOut = File.Exists(outPath)
            ? NormalizeNewlines(File.ReadAllText(outPath)).TrimEnd()
            : null;

        foreach (string line in src.Split('\n'))
        {
            string t = line.Trim();
            if (t.StartsWith("// RET:", StringComparison.OrdinalIgnoreCase))
                expRet = ParseValue(t[7..].Trim());
            else if (t.StartsWith("// STDOUT:", StringComparison.OrdinalIgnoreCase))
                expOut = t[10..].TrimEnd();
            else if (t.StartsWith("// EXPECT:", StringComparison.OrdinalIgnoreCase))
                expOut = t[10..].TrimEnd();
        }
        return (expRet, expOut);
    }

    public static (object? ret, string stdout) RunCil(string src)
    {
        MirModule mir = BuildMir(src);
        var backend = new CilBackend();

        var sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));
        try
        {
            object? ret = backend.RunMain(mir);
            return (ret, sb.ToString().TrimEnd());
        }
        finally { Console.SetOut(old); }
    }

    // --- Запуски: интерпретатор и CIL ---
    public static (object? ret, string stdout) RunInterpreter(string src)
    {
        ProgramHir hir = BuildHir(src);
        var interp = new Interpreter.Interpreter(hir);

        var sb = new StringBuilder();
        TextWriter old = Console.Out;
        Console.SetOut(new StringWriter(sb));
        try
        {
            object? ret = interp.Run();
            return (ret, sb.ToString().TrimEnd());
        }
        finally { Console.SetOut(old); }
    }

    private static MiniLangParser CreateParser(string src)
    {
        var str = new AntlrInputStream(src);
        var lexer = new Frontend.MiniLangLexer(str);
        var tokens = new CommonTokenStream(lexer);
        return new MiniLangParser(tokens);
    }

    // --- Unified assertions for interpreter / backend runs ---
    public static void AssertProgramResult(
        object? expectedRet,
        string? expectedStdout,
        object? actualRet,
        string actualStdout,
        Xunit.Abstractions.ITestOutputHelper? log = null)
    {
        if (expectedStdout is not null)
        {
            log?.WriteLine("Expected output:\n{0}", expectedStdout);
            log?.WriteLine("Actual output:\n{0}", actualStdout);
            Assert.Equal(expectedStdout, NormalizeNewlines(actualStdout).TrimEnd());
        }

        if (expectedRet is not null)
        {
            log?.WriteLine("Expected return:\n{0}", expectedRet);
            log?.WriteLine("Actual return:\n{0}", actualRet);
            AssertReturnEqual(expectedRet, actualRet);
        }
    }

    public static void RunAndAssertFile(
        string path,
        Func<string, (object? ret, string stdout)> runner,
        Xunit.Abstractions.ITestOutputHelper? log = null)
    {
        string src = File.ReadAllText(path);
        var (expRet, expOut) = ReadExpectations(path, src);
        var (ret, stdout) = runner(src);
        AssertProgramResult(expRet, expOut, ret, stdout, log);
    }

    public static void RunAndAssertSource(
        string src,
        Func<string, (object? ret, string stdout)> runner,
        string? pathForExpectations = null,
        Xunit.Abstractions.ITestOutputHelper? log = null)
    {
        var (expRet, expOut) = pathForExpectations is null
            ? (null, null)
            : ReadExpectations(pathForExpectations, src);
        var (ret, stdout) = runner(src);
        AssertProgramResult(expRet, expOut, ret, stdout, log);
    }

    public static IEnumerable<ExprHir> FlattenExpr(ExprHir? e)
    {
        if (e is null) yield break;
        yield return e;

        switch (e)
        {
            case BinHir b:
                foreach (var s in FlattenExpr(b.Left))  yield return s;
                foreach (var s in FlattenExpr(b.Right)) yield return s;
                break;
            case UnHir u:
                foreach (var s in FlattenExpr(u.Operand)) yield return s;
                break;
            case CallHir c:
                foreach (var s in FlattenExpr(c.Callee)) yield return s;
                foreach (var a in c.Args)
                foreach (var s in FlattenExpr(a)) yield return s;
                break;
            case IndexHir ix:
                foreach (var s in FlattenExpr(ix.Target)) yield return s;
                foreach (var s in FlattenExpr(ix.Index))  yield return s;
                break;
        }
    }

    public static IEnumerable<ExprHir> FlattenStmts(StmtHir s) => s switch
    {
        BlockHir b   => b.Statements.SelectMany(FlattenStmts),
        LetHir v     => v.Init is null ? Enumerable.Empty<ExprHir>() : FlattenExpr(v.Init),
        ExprStmtHir e=> e.Expr is null ? Enumerable.Empty<ExprHir>() : FlattenExpr(e.Expr),
        IfHir i      => FlattenExpr(i.Cond).Concat(FlattenStmts(i.Then))
            .Concat(i.Else is not null ? FlattenStmts(i.Else) : Enumerable.Empty<ExprHir>()),
        WhileHir w   => FlattenExpr(w.Cond).Concat(FlattenStmts(w.Body)),
        _            => Enumerable.Empty<ExprHir>()
    };

    public static void AssertSemanticOk(string src)
    {
        var input  = CharStreams.fromString(src);
        var lexer  = new MiniLangLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        var hir    = new HirBuilder().Build(parser.program());
        new SemanticChecker().Check(hir); // must not throw
    }

    public static void AssertSemanticFails(string src, string expectedSubstring)
    {
        var input  = CharStreams.fromString(src);
        var lexer  = new MiniLangLexer(input);
        var tokens = new CommonTokenStream(lexer);
        var parser = new MiniLangParser(tokens);
        var hir    = new HirBuilder().Build(parser.program());
        var ex = Assert.Throws<SemanticException>(() => new SemanticChecker().Check(hir));
        Assert.Contains(expectedSubstring, ex.Message);
    }

    public static MirFunction LowerFunction(string src, string name = "main")
    {
        var mir = BuildMir(src);
        return mir.Functions.Single(f => f.Name == name);
    }

    public static IEnumerable<MirInstr> AllMirInstructions(MirFunction f)
    {
        foreach (var b in f.Blocks)
        {
            foreach (var i in b.Instructions) yield return i;
            if (b.Terminator is not null) yield return b.Terminator;
        }
    }
}

// xUnit DataAttribute для файловых программ: [ProgramFilesData]
[AttributeUsage(AttributeTargets.Method)]
internal sealed class ProgramFilesDataAttribute : DataAttribute
{
    public override IEnumerable<object[]> GetData(MethodInfo testMethod) =>
        TestUtils.EnumerateProgramFiles();
}
