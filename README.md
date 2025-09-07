[Documentation](docs.md)

# MiniLang — Compiler, VM, and CLR Backend

Concise, end-to-end language toolchain: ANTLR → HIR → MIR → backends (CLR and custom VM), plus a tree-walking interpreter.

## What We Have
- Frontend
  - ANTLR4 grammar to syntax tree, lowered to HIR
  - Interpreter executes HIR directly
- Middle-end
  - HIR lowered to MIR
- Backends
  - MIR → CIL (runs on .NET CLR)
  - MIR → Bytecode (runs on custom stack-based VM)
- VM Runtime
  - Arrays, strings, arithmetic, control flow, built-ins (e.g., `array`, `len`)
  - Stop-the-world mark–sweep GC with tunables: threshold, growth, opportunistic auto-collect
  - GC statistics printing via `--vm-gc-stats`
- Tests
  - xUnit suite across frontend, MIR, interpreter, CLR backend, and VM

## TODO / Roadmap
- VM
  - Rewrite stack-based VM to register-based design
  - Broaden built-ins and error reporting
- Optimizations
  - Constant folding, dead-code elimination, CSE, inlining
- IR & Backends
  - Translate MIR → SSA
  - LLVM backend
- GC
  - Tune/extend heuristics (e.g., generational or byte-based thresholds)
- Tooling
  - Benchmark harness and reports for provided tasks

## Build, Test, Format
- Build: `dotnet build Compiler.sln -c Debug`
- Test: `dotnet test --collect:"XPlat Code Coverage"`
- Format: `dotnet format`

## Run
- Interpreter: `dotnet run --project Compiler.Interpreter [options] [file]`
- VM backend: `dotnet run --project Compiler.Backend.VM [options] [file]`
- CLR backend: `dotnet run --project Compiler.Backend.CLR [options] [file]`

Common options
- `-h|--help` show help; `-v|--verbose` verbose logs

VM GC options
- `--vm-gc-threshold=N` initial VM heap collection threshold (objects)
- `--vm-gc-growth=X` threshold growth factor (e.g., 1.5)
- `--vm-gc-auto=on|off` enable/disable opportunistic collections
- `--vm-gc-stats` print VM GC statistics after execution

Example GC stats output
```
[gc] mode=vm auto=on threshold=64 growth=1.5
[gc] allocations=128 collections=3 live=10 peak_live=70
```

## Goals & Requirements
- Goal: Build a language and VM with automatic memory management and a JIT-capable backend
- Language features: arithmetic, conditionals, loops, recursion
- Runtime: garbage collection; CLR JIT via CIL backend
- Correctness: three demo apps run correctly (see Tasks)

## Tasks / Benchmarks
- Task 1: Factorial Calculation (recursive)
  - Validates recursion, stack, integer ops
- Task 2: Array Sorting (e.g., quicksort/merge sort)
  - Validates arrays, loops, comparisons
- Task 3: Prime Number Generation (Sieve of Eratosthenes)
  - Validates loops, arrays, arithmetic

Benchmark targets
- Factorial(20)
- Sort 10,000 elements
- Primes up to 100,000
