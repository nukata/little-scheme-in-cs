# A Little Scheme in C# 7

This is a small interpreter of a subset of Scheme.
It implements the same language as
[little-scheme-in-python](https://github.com/nukata/little-scheme-in-python)
(and also its meta-circular interpreter, 
[little-scheme](https://github.com/nukata/little-scheme))
in circa 1,000 lines of C# 7.

As a Scheme implementation, 
it optimizes _tail calls_ and handles _first-class continuations_ properly.


## How to run

With [Mono](https://www.mono-project.com) 5.20:

```
$ csc /o /r:System.Numerics.dll arith.cs scm.cs
Microsoft (R) Visual C# Compiler version 2.8.2.62916 (2ad4aabc)
Copyright (C) Microsoft Corporation. All rights reserved.

$ mono scm.exe
> (+ 5 6)
11
> (cons 'a (cons 'b 'c))
(a b . c)
> (list
| 1
| 2
| 3
| )
(1 2 3)
> 
```

Press EOF (e.g. Control-D) to exit the session.

```
> Goodbye
$ 
```

With [.NET Core](https://github.com/dotnet/core) 2.2:

```
$ dotnet build -c Release
Microsoft (R) Build Engine version 16.1.76+g14b0a930a7 for .NET Core
Copyright (C) Microsoft Corporation. All rights reserved.

  Restore completed in 180.08 ms for /Users/suzuki/proj/little-scheme-in-cs/scm.
csproj.
  scm -> /Users/suzuki/proj/little-scheme-in-cs/bin/Release/netcoreapp2.2/scm.dl
l

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.28
$ dotnet bin/Release/netcoreapp2.2/scm.dll
> (+ 5 6)
11
> 
```

And so on.


You can run it with a Scheme script.
Examples are found in 
[little-scheme](https://github.com/nukata/little-scheme).

```
$ mono scm.exe ../little-scheme/examples/yin-yang-puzzle.scm | head

*
**
***
****
*****
******
*******
********
*********
^C
$ mono scm.exe ../little-scheme/scm.scm < ../little-scheme/examples/nqueens.scm
((5 3 1 6 4 2) (4 1 5 2 6 3) (3 6 2 5 1 4) (2 4 6 1 3 5))
$ 
```

Press INTR (e.g. Control-C) to terminate the yin-yang-puzzle.

Put a "`-`" after the script in the command line to begin a session 
after running the script.

```
$ mono scm.exe ../little-scheme/examples/fib90.scm -
2880067194370816120
> (globals)
(apply call/cc globals = < * - + symbol? eof-object? read newline display list n
ot null? pair? eqv? eq? cons cdr car fibonacci)
> (fibonacci 16)
987
> (fibonacci 1000)
43466557686937456435688527675040625802564660517371780402481729089536555417949051
89040387984007925516929592259308032263477520968962323987332247116164299644090653
3187938298969649928516003704476137795166849228875
> 
```


## The implemented language

| Scheme Expression                   | Internal Representation             |
|:------------------------------------|:------------------------------------|
| numbers `1`, `2.3`                  | `int`, `double` or `BigInteger`     |
| `#t`                                | `true`                              |
| `#f`                                | `false`                             |
| strings `"hello, world"`            | `string`                            |
| symbols `a`, `+`                    | `class Sym`                         |
| `()`                                | `null`                              |
| pairs `(1 . 2)`, `(x y z)`          | `class Cell`                        |
| closures `(lambda (x) (+ x 1))`     | `class Closure`                     |
| built-in procedures `car`, `cdr`    | `class Intrinsic`                   |
| continuations                       | `class Continuation`                |


For expression types and built-in procedures, see
[little-scheme-in-python](https://github.com/nukata/little-scheme-in-python).
