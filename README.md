[Documentation](docs.md)

# What we have:
- ANTLR-4 syntax parse tree -> HIR
- *HIR may be run on interpreter*
- HIR -> MIR
- MIR -> CIL
- *CIL may be run on CLR*

# TOOD:
- Translate MIR -> SSA
- Run on LLVM
- Optimizations
- Write own backend (?)

## Goal:
• Develop own language and virtual machine with automatic memory management and JIT compiler
## Language Requirements:
•	Support of basic arithmetic operations
•	Conditional operators (if-else)
•	Loops (for, while)
•	Recursion
## Runtime Requirements:
•	Garbage Collector
•	JIT compiler
•	Executed applications must perform correctly
•	Any technology is allowed, no restrictions
## Correctness:
• Demonstrate that 3 applications work correctly:
>         Factorial calculation
>         Array sorting
>         Prime number generation
Performance:
•	Factorial of 20
•	Sort array of 10000 elements
•	Prime numbers up to 100000
Marks:
•	4 – demonstrate benchmark execution
•	5 – develop benchmark (Task 4) in 2 hours timeframe,
 using your language and successfully execute it


# Tasks:
## Task 1: Factorial Calculation (Recursive Function)

 Objective: Implement a function that calculates the factorial of a given number using recursion.

 Purpose: Test recursive function calls, stack management, and handling of large numbers.
 Benchmark: Measure the execution time for calculating the factorial of numbers like 10! or 20!.

 ---

 ## Task 2: Array Sorting (Sorting Algorithm)

 Objective: Implement an algorithm to sort an array (e.g., quicksort or merge sort).

 Purpose: Test array handling, loop operations, and element comparison.
 Benchmark: Measure the time taken to sort a randomly generated array of, for example, 1000 or 10,000 elements.

 ---

 ## Task 3: Prime Number Generation (Sieve of Eratosthenes)

 Objective: Implement an algorithm to generate prime numbers (e.g., Sieve of Eratosthenes).

 Purpose: Test array manipulation, loops, and arithmetic operations.
 Benchmark: Measure the time it takes to generate all prime numbers up to 100,000.

