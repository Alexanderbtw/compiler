using System;

namespace Compiler.Translation.Semantic.Exceptions;

public class SemanticException(string msg) : Exception(msg);
