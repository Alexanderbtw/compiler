using System;

namespace Compiler.Frontend.Semantic.Exceptions;

internal class SemanticException(string msg) : Exception(msg);
