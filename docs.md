1 · Lexical structure

Token	Example(s)	Notes
Identifiers	foo, bar42	ASCII letters / digits / _must not start with a digit.
Integer literal	0, -42, 1234567890	64-bit signed (long).
Char literal	'a', '\n', '\\'	Simple C-style escapes.
String literal	"hello", "\"quoted\""	Backslash escapes \" and \\.
Keywords	fn var if else while for break continue return true false	
Operators	`+ - * / % < <= > >= == != &&	
Punctuation	, ; ( ) { } [ ]	
Comments	// to EOL	Comments are ignored by the lexer.


⸻

2 · Types & values

MiniLang is dynamically typed at run-time but has a small, fixed
runtime universe:

Runtime tag	Literal(s)	Description
long	42	64-bit signed integer.
bool	true false	Logical value.
char	'x'	16-bit Unicode scalar.
string	"foo"	Immutable sequence of UTF-16 code units.
array	(none)	Mutable, zero-indexed array of object?; elements default to null.
null	(none)	The absence of a value.


⸻

3 · Expressions

postfixExpr                calls & indexing
unary           + - !
multiplication  * / %
addition        + -
comparison      < <= > >=
equality        == !=
logicalAnd      &&
logicalOr       ||
assignment      =

	•	All binary operators are left-associative.
	•	Logical && / || are short-circuiting.
	•	Result of = is the assigned value (C-style).
	•	Truthiness (if (expr)) is:
	•	bool – its own value,
	•	long – non-zero,
	•	everything else – not null.

⸻

4 · Statements

statement
    : variableDecl
    | ifStmt
    | whileStmt
    | forStmt
    | breakStmt
    | continueStmt
    | returnStmt
    | exprStmt
    | block

4.1 Variable declaration

var i = 0;
var empty;           // initialises to null

Variables are function-local (no global vars).

4.2 Control flow

if (cond) stmt1 else stmt2
while (cond) stmt
for (init; cond; iter) stmt
break;      // jumps out of innermost loop
continue;   // jumps to loop header / iterator
return expr?;

Semantics mimic C/Java, except that the loop parts in for (...) are
full expression lists (a = a + 1, b = b + 2 is legal).

⸻

5 · Functions

fn gcd(a, b) {
    while (b != 0) {
        var t = b;
        b = a % b;
        a = t;
    }
    return a;
}

	•	All functions implicitly return null if execution falls off the end.
	•	No overloading; one global namespace of function names.
	•	Tail-calls not guaranteed to be optimised away (depends on backend).

main() must exist and takes no parameters; it is the program entry.

⸻

6 · Arrays

var a = array(10);   // built-in factory, 10 nulls
a[0] = 42;
print(a[0]);         // 42

Bounds are checked at run-time. On violation a
RuntimeException: array index out of bounds is raised.

⸻

7 · Built-in functions

Name	Arity	Behaviour
array(n)	1	Returns a new array of length n (elements = null).
print(x)	1	Writes a textual representation of x followed by \n.
len(arr) (opt.)	1	Returns array length. (Add if desired.)

Back-end mappings:

MiniLang	IL (System)	C (libc)
print	Console.WriteLine	printf("%lld\n", v) etc.
array	new object?[n]	malloc(n * sizeof(void*))


⸻

8 · Complete example programs

8.1 Factorial (recursive)

fn fact(n) {
    if (n <= 1) return 1;
    return n * fact(n - 1);
}

fn main() {
    print(fact(10));    // 3628800
}

8.2 Quick-sort

fn qsort(arr, lo, hi) {
    if (lo >= hi) return;
    var p = arr[(lo + hi) / 2];
    var i = lo;
    var j = hi;
    while (i <= j) {
        while (arr[i] < p)  i = i + 1;
        while (arr[j] > p)  j = j - 1;
        if (i <= j) {
            var t = arr[i]; arr[i] = arr[j]; arr[j] = t;
            i = i + 1;  j = j - 1;
        }
    }
    qsort(arr, lo, j);
    qsort(arr, i,  hi);
}

fn main() {
    var a = array(10);
    var k = 0;
    while (k < 10) { a[k] = 9 - k; k = k + 1; }
    qsort(a, 0, 9);
    k = 0;
    while (k < 10) { print(a[k]); k = k + 1; }
}

8.3 Primes (Sieve of Eratosthenes)

fn sieve(n) {
    var flags = array(n + 1);
    var p = 2;
    while (p * p <= n) {
        if (flags[p] == null) {          // unmarked ⇒ prime
            var k = p * p;
            while (k <= n) { flags[k] = true; k = k + p; }
        }
        p = p + 1;
    }
    var i = 2;
    while (i <= n) { if (flags[i] == null) print(i); i = i + 1; }
}

fn main() { sieve(100000); }


⸻

9 · Toolchain quick-start

# interpret
mini run prog.ml        # uses tree-walking interpreter

# emit TAC for inspection
mini tac prog.ml > prog.tac

# compile to .NET IL and run
mini build prog.ml -o prog.exe
./prog.exe

Options:

--time          measure wall-clock time
--emit-il       dump IL (or --emit-c / --emit-asm depending on backend)
--O1/--O2       enable optimisation passes


⸻

10 · Diagnostic messages

Phase	Example
Lexing	line 3: unterminated string literal
Parsing	line 7: mismatched input '}' expecting ';'
Semantic check	identifier 'i' not in scope
Runtime (interp)	array index out of bounds
Code-gen	function 'main' must take 0 parameters

All errors abort compilation; the CLI returns exit-code 1.

⸻

11 · Implementation notes (for contributors)
	•	AST: immutable C# record classes in Frontend/AST.
	•	Visitor → TAC lowering pass in Backend/Lowering.
	•	Interpreter: Frontend/Interpretation/Interpreter.cs.
	•	Semantic checker: symbol tables + scope stack in Frontend/Semantics/SemanticChecker.cs.
	•	Unit-tests: xUnit; see Tests/ for AST-build, semantic, interpreter and compiler suites.
	•	Coding style: dotnet format (Microsoft conventions).