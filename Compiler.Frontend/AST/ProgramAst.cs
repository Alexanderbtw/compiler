using System.Collections.Generic;

using Compiler.Frontend.AST.Statements;

namespace Compiler.Frontend.AST;

public record ProgramAst(List<FuncDef> Functions);
