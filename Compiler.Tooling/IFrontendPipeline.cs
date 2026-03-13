using Compiler.Frontend.Translation.HIR.Common;
using Compiler.Frontend.Translation.MIR.Common;

namespace Compiler.Tooling;

public interface IFrontendPipeline
{
    ProgramHir BuildHir(
        string src,
        bool verbose = false);

    MirModule BuildMir(
        ProgramHir hir);
}
