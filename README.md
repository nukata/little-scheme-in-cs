# A Little Scheme in C#

This is a small interpreter of a subset of Scheme
in circa 1,000 lines of _C# 7_
(including a small arithmetic library
[`arith.cs`](arith.cs) in circa 300 lines).
It implements the same language as

- [little-scheme-in-python](https://github.com/nukata/little-scheme-in-python)
- [little-scheme-in-go](https://github.com/nukata/little-scheme-in-go)
- [little-scheme-in-java](https://github.com/nukata/little-scheme-in-java)

and their meta-circular interpreter, 
[little-scheme](https://github.com/nukata/little-scheme).

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

  Restore completed in 176.43 ms for /Users/suzuki/proj/little-scheme-in-cs/scm.
csproj.
  scm -> /Users/suzuki/proj/little-scheme-in-cs/bin/Release/netcoreapp2.2/scm.dl
l

Build succeeded.
    0 Warning(s)
    0 Error(s)

Time Elapsed 00:00:01.25
$ dotnet bin/Release/netcoreapp2.2/scm.dll
> (+ 5 6)
11
> 
```

And so on.


You can run it with a Scheme script.
Examples are found in 
[little-scheme](https://github.com/nukata/little-scheme);
download it at `..` and you can try the following:


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
$ mono scm.exe ../little-scheme/examples/amb.scm
((1 A) (1 B) (1 C) (2 A) (2 B) (2 C) (3 A) (3 B) (3 C))
$ mono scm.exe ../little-scheme/examples/nqueens.scm
((5 3 1 6 4 2) (4 1 5 2 6 3) (3 6 2 5 1 4) (2 4 6 1 3 5))
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
(apply call/cc globals error = < * - + symbol? eof-object? read newline display
list not null? pair? eqv? eq? cons cdr car fibonacci)
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


The implementation is similar to those of
[little-scheme-in-dart](https://github.com/nukata/little-scheme-in-dart) and
[little-scheme-in-java](https://github.com/nukata/little-scheme-in-java).


### Expression types

- _v_  [variable reference]

- (_e0_ _e1_...)  [procedure call]

- (`quote` _e_)  
  `'`_e_ [transformed into (`quote` _e_) when read]

- (`if` _e1_ _e2_ _e3_)  
  (`if` _e1_ _e2_)

- (`begin` _e_...)

- (`lambda` (_v_...) _e_...)

- (`set!` _v_ _e_)

- (`define` _v_ _e_)

For simplicity, this Scheme treats (`define` _v_ _e_) as an expression type.


### Built-in procedures

|                      |                          |                     |
|:---------------------|:-------------------------|:--------------------|
| (`car` _lst_)        | (`not` _x_)              | (`eof-object?` _x_) |
| (`cdr` _lst_)        | (`list` _x_ ...)         | (`symbol?` _x_)     |
| (`cons` _x_ _y_)     | (`call/cc` _fun_)        | (`+` _x_ _y_)       |
| (`eq?` _x_ _y_)      | (`apply` _fun_ _arg_)    | (`-` _x_ _y_)       |
| (`eqv?` _x_ _y_)     | (`display` _x_)          | (`*` _x_ _y_)       |
| (`pair?` _x_)        | (`newline`)              | (`<` _x_ _y_)       |
| (`null?` _x_)        | (`read`)                 | (`=` _x_ _y_)       |
|                      | (`error` _reason_ _arg_) | (`globals`)         |

- `(error` _reason_ _arg_`)` throws an exception with the message
  "`Error:` _reason_`:` _arg_".
  It is based on [SRFI-23](https://srfi.schemers.org/srfi-23/srfi-23.html).

- `(globals)` returns a list of keys of the global environment.
  It is not in the standard.

See [`GlobalEnv`](scm.cs#L338-L392)
in `scm.cs` for the implementation of the procedures
except `call/cc` and `apply`.  
`call/cc` and `apply` are implemented particularly at 
[`ApplyFunction`](scm.cs#L528-L565) in `scm.cs`.

I hope this serves as a model of how to write a Scheme interpreter in C#.
