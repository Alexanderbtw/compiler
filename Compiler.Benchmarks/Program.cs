using BenchmarkDotNet.Running;

namespace Compiler.Benchmarks;

public static class Program
{
    public static void Main(
        string[] args)
    {
        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(args);
    }
}
