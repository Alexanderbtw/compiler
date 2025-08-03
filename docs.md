<pre>
# MiniLang Documentation

MiniLang is a deliberately minimal, C-style teaching language used to demonstrate the full compiler tool-chain: **lexer → parser → AST → semantic checks → interpreter → code-gen**.  
It supports integers, booleans, chars, strings, first-class arrays and user-defined functions. There is **no static typing** — all values are boxed at runtime.

---

## 1 · “Hello, world!”

```ml
fn main() {
    print("Hello, MiniLang!");
}
```

---

## 2 · Lexical structure

| Element            | Example(s)               | Notes                               |
| ------------------ | ------------------------ | ----------------------------------- |
| **Identifiers**    | `counter`, `my_var`      | ASCII letters, digits, `_` — cannot start with digit |
| **Integer lit.**   | `0`, `42`, `-9000`       | 64-bit signed                       |
| **Char lit.**      | `'a'`, `'\n'`            | single quotes                       |
| **String lit.**    | `"abc"`, `"foo\nbar"`    | double quotes, supports `\"` `\\`   |
| **Keywords**       | `fn var if else while for break continue return true false` |
| **Operators**      | `+ - * / % < <= > >= == != && || =` |
| **Delimiters**     | `()` `[]` `{}` `, ;`     |
| **Comments**       | `// to end-of-line`      | (single-line only)                  |

---

## 3 · Grammar (EBNF)

```ebnf
program         = { functionDecl } ;
functionDecl    = "fn" ID "(" [ paramList ] ")" block ;
paramList       = ID { "," ID } ;

block           = "{" { statement } "}" ;

statement       = variableDecl
                | ifStmt | whileStmt | forStmt
                | breakStmt | continueStmt | returnStmt
                | exprStmt | block ;

variableDecl    = "var" ID [ "=" expression ] ";" ;
ifStmt          = "if" "(" expression ")" statement [ "else" statement ] ;
whileStmt       = "while" "(" expression ")" statement ;
forStmt         = "for" "(" ( variableDecl | [ expressionList ] ";" )
                  [ expression ] ";" [ expressionList ] ")" statement ;
breakStmt       = "break" ";" ;
continueStmt    = "continue" ";" ;
returnStmt      = "return" [ expression ] ";" ;
exprStmt        = [ expression ] ";" ;
expressionList  = expression { "," expression } ;

expression      = assignment ;
assignment      = postfixExpr "=" assignment | logicalOr ;

logicalOr       = logicalAnd { "||" logicalAnd } ;
logicalAnd      = equality   { "&&" equality   } ;
equality        = comparison { ("==" | "!=") comparison } ;
comparison      = addition   { ("<" | "<=" | ">" | ">=") addition } ;
addition        = multiplication { ("+" | "-") multiplication } ;
multiplication  = unary { ("*" | "/" | "%") unary } ;
unary           = ( "+" | "-" | "!" ) unary | postfixExpr ;

postfixExpr     = primary { callSuffix | indexSuffix } ;
callSuffix      = "(" [ argumentList ] ")" ;
indexSuffix     = "[" expression "]" ;
argumentList    = expression { "," expression } ;

primary         = INT | STRING | CHAR | TRUE | FALSE | ID | "(" expression ")" ;
```

---

## 4 · Built-in functions

| Name           | Arity | Description                                 |
| -------------- | ----- | ------------------------------------------- |
| `print`        | 1     | Writes value followed by newline            |
| `array`        | 1     | Allocates fresh array of length **n** (filled with `null`) |
| `time_ms`      | 0     | Returns current wall clock time (ms)        |
| `rand`         | 0     | Pseudo-random 64-bit integer                |

---

## 5 · Semantics

* **Values** — Boxed `object?`.  
  Only `long`, `bool`, `char`, `string`, `object?[]` and `null` appear at runtime.
* **Arithmetic** — Operands are converted with `ToLong`; `null` is illegal.
* **Truthiness** — `bool` → itself, `long` → `n ≠ 0`, others → non-null.
* **Arrays** — Zero-based, bounds-checked; elements default to `null`.
* **Variables** — Dynamically scoped inside each function frame.
* **Functions** — Always return `object?` (default `null`). Recursive calls allowed.
* **Control flow** — `break`/`continue` only valid inside `while`/`for`.

---

## 6 · Examples

### 6.1 · Recursive factorial

```ml
fn fact(n) {
    if (n <= 1) return 1;
    return n * fact(n - 1);
}

fn main() {
    var i = 10;
    print(fact(i));
}
```

### 6.2 · In-place quick-sort

```ml
fn qsort(arr, lo, hi) {
    if (lo >= hi) return;
    var p = arr[(lo + hi) / 2];
    var i = lo;
    var j = hi;
    while (i <= j) {
        while (arr[i] < p) i = i + 1;
        while (arr[j] > p) j = j - 1;
        if (i <= j) {
            var t = arr[i]; arr[i] = arr[j]; arr[j] = t;
            i = i + 1; j = j - 1;
        }
    }
    qsort(arr, lo, j);
    qsort(arr, i, hi);
}

fn main() {
    var a = array(10);
    var k = 0;
    while (k < 10) { a[k] = 9 - k; k = k + 1; }
    qsort(a, 0, 9);
    k = 0; while (k < 10) { print(a[k]); k = k + 1; }
}
```

### 6.3 · Prime sieve

```ml
fn sieve(limit) {
    var isPrime = array(limit + 1);
    var i = 2;
    while (i <= limit) { isPrime[i] = true; i = i + 1; }

    var p = 2;
    while (p * p <= limit) {
        if (isPrime[p]) {
            var j = p * p;
            while (j <= limit) { isPrime[j] = false; j = j + p; }
        }
        p = p + 1;
    }

    i = 2; while (i <= limit) { if (isPrime[i]) print(i); i = i + 1; }
}

fn main() { sieve(100); }
```

---

## 7 · Tool-chain stages

| Stage             | Output                              | Purpose                                  |
| ----------------- | ----------------------------------- | ---------------------------------------- |
| **Lexer**         | token stream                        | split into keywords, identifiers, …      |
| **Parser**        | concrete syntax tree (ANTLR)        | enforce grammar                          |
| **AST builder**   | cleaned abstract syntax tree        | remove tokens, keep structure            |
| **Semantic check**| annotated AST                       | scope, arity, control-flow validity      |
| **Interpreter**   | value                               | fast turnaround while compiler matures   |
| **Lowering**      | three-address code (TAC)            | simplifies code-gen + enables optims     |
| **Back-end**      | IL / C / ASM / LLVM                | executable program                       |

Run all stages via CLI:

```bash
minic --emit-ast   prog.ml      # pretty-print AST
minic --check      prog.ml      # semantic only
minic --run        prog.ml      # interpret
minic --emit-tac   prog.ml      # dump lowered IR
minic -o prog.exe  prog.ml      # compile to native/IL and link
```

---

## 8 · Future ideas

* Static type inference (Hindley-Milner lite).
* First-class tuples & pattern matching.
* SSA + register allocation backend.
* REPL with incremental compilation.

---

### Changelog

| Version | Date       | Notes                                       |
| ------- | ---------- | ------------------------------------------- |
| 0.1     | 2025-08-02 | Initial public draft                        |
</pre>
