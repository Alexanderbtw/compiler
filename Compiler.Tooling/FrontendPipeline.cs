using System.Diagnostics;

using Antlr4.Runtime;

using Compiler.Frontend;
using Compiler.Frontend.Translation.HIR;
using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.HIR.Semantic;
using Compiler.Frontend.Translation.MIR;
using Compiler.Frontend.Translation.MIR.Common;
using Compiler.Frontend.Translation.MIR.Optimization;
using Compiler.Tooling.Diagnostics;

using Microsoft.Extensions.Logging;

namespace Compiler.Tooling;

/// <summary>
///     Thin orchestrator from source text to HIR/MIR.
///     Adds ANTLR error listeners, runs semantic checks, then MIR lowering and light MIR passes.
/// </summary>
public sealed class FrontendPipeline(
    ILogger<FrontendPipeline> logger) : IFrontendPipeline
{
    public ProgramHir BuildHir(
        string src,
        bool verbose = false)
    {
        using Activity? activity = CompilerInstrumentation.ActivitySource.StartActivity("frontend.build-hir");
        var parseWatch = Stopwatch.StartNew();

        ProgramHir hir;
        bool hadParseError;

        {
            var str = new AntlrInputStream(src);
            var lexer = new MiniLangLexer(str);
            var tokens = new CommonTokenStream(lexer);
            var parser = new MiniLangParser(tokens);

            var listenerLexer = new ErrorListener<int>();
            var listenerParser = new ErrorListener<IToken>();
            lexer.RemoveErrorListeners();
            parser.RemoveErrorListeners();
            lexer.AddErrorListener(listenerLexer);
            parser.AddErrorListener(listenerParser);

            MiniLangParser.ProgramContext? tree = parser.program();
            hadParseError = listenerLexer.HadError || listenerParser.HadError;

            if (hadParseError)
            {
                IReadOnlyList<string> diagnostics = listenerLexer
                    .Diagnostics
                    .Concat(listenerParser.Diagnostics)
                    .Distinct(StringComparer.Ordinal)
                    .ToArray();

                throw new MiniLangSyntaxException(
                    string.Join(
                        separator: Environment.NewLine,
                        values: diagnostics));
            }

            hir = new HirBuilder().Build(tree);
        }

        parseWatch.Stop();
        CompilerInstrumentation.ParseDurationMs.Record(parseWatch.Elapsed.TotalMilliseconds);

        if (verbose)
        {
            if (hadParseError)
            {
                logger.LogWarning("Parse failed");
            }
            else
            {
                logger.LogInformation("Parse succeeded");
            }
        }

        var semanticWatch = Stopwatch.StartNew();
        new SemanticChecker().Check(hir);
        semanticWatch.Stop();
        CompilerInstrumentation.SemanticDurationMs.Record(semanticWatch.Elapsed.TotalMilliseconds);

        return hir;
    }

    public MirModule BuildMir(
        ProgramHir hir,
        MirOptimizationOptions options)
    {
        using Activity? activity = CompilerInstrumentation.ActivitySource.StartActivity("frontend.build-mir");
        var loweringWatch = Stopwatch.StartNew();
        MirModule mir = new HirToMir().Lower(hir);
        loweringWatch.Stop();
        CompilerInstrumentation.LoweringDurationMs.Record(loweringWatch.Elapsed.TotalMilliseconds);

        var optimizeWatch = Stopwatch.StartNew();
        new MirPassManager().Run(
            module: mir,
            options: options,
            passObserver: (passName, _, durationMs) =>
            {
                CompilerInstrumentation.PassDurationMs.Record(
                    value: durationMs,
                    tagList: new TagList
                    {
                        { "pass", passName }
                    });
            });
        optimizeWatch.Stop();
        CompilerInstrumentation.OptimizationDurationMs.Record(optimizeWatch.Elapsed.TotalMilliseconds);

        // Experimental type annotator intentionally stays out of the default pipeline.
        // See Compiler.Frontend.Translation/Experimental/Typing.

        return mir;
    }
}
