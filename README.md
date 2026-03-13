[Documentation](docs.md)

# MiniLang

MiniLang is an internal compiler/toolchain solution built around a single pipeline:
ANTLR syntax tree -> HIR -> MIR -> execution hosts. The repository currently ships
an interpreter, a CIL backend that targets the shared VM runtime contracts, a custom
VM runtime with GC, shared tooling/hosting infrastructure, and an isolated
`Experimental/Typing` area for incomplete type-system work.

## Solution Layout

- `Compiler.Frontend`
  Grammar, lexer/parser generation, syntax entrypoints.
- `Compiler.Frontend.Translation`
  HIR/MIR models, lowering, semantics, builtins metadata, and `Experimental/Typing`.
- `Compiler.Backend.JIT.Abstractions`
  Backend/runtime execution contracts shared by compiled backends.
- `Compiler.Backend.JIT.CIL`
  CIL backend host and JIT implementation.
- `Compiler.Runtime.VM`
  VM runtime, values, arrays, GC, and builtin runtime support.
- `Compiler.Interpreter`
  Tree-walking interpreter host.
- `Compiler.Tooling`
  Shared host orchestration, pipeline wiring, command options, logging, and diagnostics.
- `Compiler.Benchmarks`
  BenchmarkDotNet harness for factorial, sorting, and prime-sieve workloads.
- `Compiler.Tests`
  xUnit regression, parity, architecture, CLI, and documentation checks.

## Build, Test, Format

- Build: `dotnet build Compiler.sln -c Debug`
- Test: `dotnet test Compiler.sln`
- Format: `dotnet format`

Solution-wide SDK settings live in `Directory.Build.props`, package versions in
`Directory.Packages.props`, and the pinned SDK in `global.json`.

## Run

The executable hosts use `System.CommandLine` with a `run` subcommand and are wired
through `Microsoft.Extensions.Hosting`.

- Interpreter:
  `dotnet run --project Compiler.Interpreter -- run --file Compiler.Tests/Tasks/factorial_calculation.minl`
- CIL backend:
  `dotnet run --project Compiler.Backend.JIT.CIL -- run --file Compiler.Tests/Tasks/factorial_calculation.minl`

Common options:

- `-f|--file` path to `.minl` source
- `-v|--verbose` verbose compiler/runtime logging
- `--quiet` suppress builtin stdout such as `print`
- `--time` emit total execution time

VM GC options for the CIL host:

- `--vm-gc-threshold <N>` initial heap collection threshold
- `--vm-gc-growth <X>` threshold growth factor
- `--vm-gc-auto <on|off>` enable or disable opportunistic collections
- `--vm-gc-stats` print GC statistics after execution

## Benchmarks

Run the benchmark harness in Release mode:

- `dotnet run --project Compiler.Benchmarks -c Release`

The harness compares interpreter and CIL execution on:

- recursive factorial
- array sorting
- prime number generation

## Current Architecture Notes

- `Compiler.Tooling` is intentionally limited to host/orchestration concerns. Core
  language semantics stay in `Compiler.Frontend.Translation`.
- `Compiler.Backend.JIT.Abstractions` does not depend on the VM implementation;
  `Compiler.Runtime.VM` is now a concrete runtime.
- `Experimental/Typing` is a deliberate WIP zone and is not part of the default
  compilation pipeline.
