# MiniLang – Reference Manual

MiniLang is a tiny, dynamically typed, C‑style language that drives a full pipeline: lexer → parser → HIR → MIR → backends (CLR and a custom VM), plus a tree‑walking interpreter. Program files use the extension `.minl`.

Backends & running
- Interpreter: `dotnet run --project Compiler.Interpreter [options] [file]`
- CLR backend: `dotnet run --project Compiler.Backend.CLR [options] [file]`
- VM backend: `dotnet run --project Compiler.Backend.VM [options] [file]`
- Useful options: `-v|--verbose`, `-h|--help`
- VM GC: `--vm-gc-threshold=N`, `--vm-gc-growth=X`, `--vm-gc-auto=on|off`, `--vm-gc-stats`

---

## 1 · Lexical structure

| Token            | Example(s)            | Notes                                                             |
|------------------|-----------------------|-------------------------------------------------------------------|
| **Identifiers**  | `foo`, `bar42`        | ASCII letters / digits / `_`, must **not** start with a digit     |
| **Integer lit.** | `0`, `-42`, `123456`  | 64-bit signed (`long`)                                            |
| **Char lit.**    | `'a'`, `'\n'`, `'\\'` | Simple C-style escapes                                            |
| **String lit.**  | `"hi"`, `"\"x\""`     | Back-slash escapes `\"` and `\\`                                  |
| **Keywords**     | `fn`, `var`, `if`, `else`, `while`, `for`, `break`, `continue`, `return`, `true`, `false` |
| **Operators**    | `+ - * / % < <= > >= == != && || ! =`             | All left-associative                                               |
| **Punctuation**  | `, ; ( ) { } [ ]`     |                                                                   |
| **Comments**     | `// to end of line`   | Discarded by the lexer                                            |

---

## 2 · Types & values (run-time)

| Tag    | Literal(s) | Description                                                     |
|--------|------------|-----------------------------------------------------------------|
| `long` | `42`       | 64-bit signed integer                                           |
| `bool` | `true`     | Logical value                                                   |
| `char` | `'x'`      | 16-bit Unicode scalar                                           |
| `string` | `"foo"`  | Immutable UTF-16 sequence                                       |
| `array`| —          | Mutable, zero-indexed `object?[]`, elements default to `null`   |
| `null` | —          | Absence of a value                                              |

---

## 3 · Expressions (precedence / associativity)

```
postfixExpr      calls & indexing      (left)
unary            +  -  !               (right)
multiplication   *  /  %               (left)
addition         +  -                  (left)
comparison       <  <= > >=            (left)
equality         == !=                 (left)
logicalAnd       &&                    (left, short-circuit)
logicalOr        ||                    (left, short-circuit)
assignment       =                     (right)  value of expr is RHS
```

Truthiness (used by `if`, `while`, …)

* `bool` → itself
* `long` → non‑zero
* `null` → false
* Strings/arrays: VM/CLR treat empty as false, non‑empty as true; interpreter treats any non‑null as true

---

## 4 · Statements

```
var i = 0;                // variableDecl
if (cond) stmt            // ifStmt (+ optional else)
while (cond) stmt         // whileStmt
for (init; cond; iter) stmt
break; continue;          // loop control
return expr?;             // returnStmt
expr;                     // exprStmt (may be empty)
{ ... }                   // block
```

All variables are *function-local*; there are no globals.

---

## 5 · Functions

```minl
fn gcd(a, b) {
    while (b != 0) {
        var t = b;
        b = a % b;
        a = t;
    }
    return a;
}
```

* No overloading; a single global namespace of functions.  
* If execution “falls off” the end, the function yields `null`.  
* Exactly one entry function **`main()`** with zero parameters is required.

---

## 6 · Arrays

```minl
var a = array(5);  // 5 nulls
a[0] = 123;
print(a[0]);       // 123
```

Indices are bounds‑checked; out‑of‑range access raises a runtime error.

---

## 7 · Built‑in functions

| Name             | Arity   | Behaviour                                                                 |
|------------------|---------|---------------------------------------------------------------------------|
| `array(n)`       | 1       | Fresh array of length n (all elements `null`). VM lowers to `NewArr`.     |
| `array(n, init)` | 2       | Interpreter/CLR only: fills with `init`. VM currently supports 1‑arg form |
| `print(x, …)`    | varargs | Writes space‑separated values and a newline                                |
| `len(x)`         | 1       | Length of string or array (returns `long`)                                 |
| `ord(c)`         | 1       | Code point of `char` or 1‑length string (returns `long`)                  |
| `chr(i)`         | 1       | `char` for integer code point (range‑checked)                              |
| `assert(cond, msg?)` | 1–2 | Throws on false; optional message                                          |
| `clock_ms()`     | 0       | Elapsed milliseconds (monotonic)                                           |

---

## 8 · Complete example programs

### 8.1 Factorial (recursive) – `fact.minl`

```minl
fn fact(n) {
    if (n <= 1) return 1;
    return n * fact(n - 1);
}

fn main() {
    print(fact(10));  // 3628800
}
```

### 8.2 Quick-sort – `qsort.minl`

```minl
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

### 8.3 Prime sieve – `sieve.minl`

```minl
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
