using System;

namespace Compiler.Frontend.Semantic;

internal class SemanticException(string msg) : Exception(msg);
