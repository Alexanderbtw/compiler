[Documentation](docs.md)

# MiniLang

MiniLang is an internal compiler/toolchain solution built around a single pipeline:
ANTLR syntax tree -> HIR -> MIR -> execution hosts. The repository currently ships
an interpreter, a register-VM backend, a custom
VM runtime with GC, shared tooling/hosting infrastructure, and an isolated
`Experimental/Typing` area for incomplete type-system work.

## Solution Layout

- `Compiler.Frontend`
  Grammar, lexer/parser generation, syntax entrypoints.
- `Compiler.Frontend.Translation`
  HIR/MIR models, lowering, semantics, builtins metadata, and `Experimental/Typing`.
- `Compiler.Backend.JIT.Abstractions`
  Backend/runtime execution contracts shared by compiled backends.
- `Compiler.Backend.VM`
  Register-VM backend host and MIR-to-bytecode compiler.
- `Compiler.Runtime.VM`
  VM runtime, values, heap, GC, and builtin runtime support.
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
`Directory.Packages.props`. The repository currently targets `.NET 10`
(`net10.0`).

## Run

The executable hosts use `System.CommandLine` with a `run` subcommand and are wired
through `Microsoft.Extensions.Hosting`.

- Interpreter:
  `dotnet run --project Compiler.Interpreter -- run --file Compiler.Tests/Tasks/factorial_calculation.minl`
- VM backend:
  `dotnet run --project Compiler.Backend.VM -- run --file Compiler.Tests/Tasks/factorial_calculation.minl`

Common options:

- `-f|--file` path to `.minl` source
- `-v|--verbose` verbose compiler/runtime logging
- `--quiet` suppress builtin stdout such as `print`
- `--time` emit total execution time

VM GC options for the VM host:

- `--gc-threshold <N>` initial heap collection threshold
- `--gc-growth <X>` threshold growth factor
- `--gc-auto <on|off>` enable or disable opportunistic collections
- `--gc-stats` print GC statistics after execution

## Benchmarks

Run the benchmark harness in Release mode:

- `dotnet run --project Compiler.Benchmarks -c Release`

The harness compares interpreter and VM execution on:

- recursive factorial
- array sorting
- prime number generation

## Current Architecture Notes

- `Compiler.Tooling` is intentionally limited to host/orchestration concerns. Core
  language semantics stay in `Compiler.Frontend.Translation`.
- `Compiler.Backend.JIT.Abstractions` contains only the generic MIR-to-backend
  compile contract. VM execution stays in `Compiler.Backend.VM`.
- The VM backend is the primary compiled execution target. The interpreter is kept
  as a separate execution host and parity baseline in tests.
- The runtime builtin surface currently includes `array`, `print`, `assert`,
  `len`, `ord`, `chr`, and `clock_ms`.
- `Experimental/Typing` is a deliberate WIP zone and is not part of the default
  compilation pipeline.
