// A little Scheme in C# 7, v1.0.2 R01.07.14/R02.03.20 by SUZUKI Hisao
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using LittleArith;

// scm.exe: csc -doc:scm.xml -o -r:System.Numerics.dll scm.cs arith.cs
// doc: mdoc update -i scm.xml -o xml scm.exe; mdoc export-html -o html xml

namespace LittleScheme {
    /// <summary>Cons cell</summary>
    public sealed class Cell: IEnumerable<object> {
        /// <summary>Head part of the cell</summary>
        public readonly object Car;
        /// <summary>Tail part of the cell</summary>
        public object Cdr;

        /// <summary>Construct a cons cell with its head and tail.</summary>
        public Cell(object car, object cdr) {
            Car = car;
            Cdr = cdr;
        }
 
        /// <summary>Yield car, cadr, caddr and so on.</summary>
        /// <exception cref="ImproperListException">The list ends with
        /// a non-null value.</exception>
       public IEnumerator<object> GetEnumerator() {
            object j = this;
            while (j is Cell jc) {
                yield return jc.Car;
                j = jc.Cdr;
            }
            if (j != null)
                throw new ImproperListException(j);
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
    }

    /// <summary>The last tail of the list is not null.</summary>
    public class ImproperListException: Exception {
        /// <summary>The last tail of the list</summary>
        public readonly object Tail;

        /// <summary>Construct with the last tail which is not null.</summary>
        public ImproperListException(object tail) {
            Tail = tail;
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>Scheme's symbol</summary>
    public sealed class Sym {
        /// <summary>The symbol's name</summary>
        private readonly string Name;

        /// <summary>Construct a symbol that is not interned yet.</summary>
        private Sym(string name) {
            Name = name;
        }

        /// <summary>Return the symbol's name</summary>
        public override string ToString() => Name;

        /// <summary>Table of interned symbols</summary>
        internal static readonly Dictionary<string, Sym> Symbols =
            new Dictionary<string, Sym>();

        /// <summary>Construct an interned symbol.</summary>
        public static Sym New(string name) {
            Symbols.TryAdd(name, new Sym(name));
            return Symbols[name];
        }

        internal static readonly Sym Quote = New("quote");
        internal static readonly Sym If = New("if");
        internal static readonly Sym Begin = New("begin");
        internal static readonly Sym Lambda = New("lambda");
        internal static readonly Sym Define = New("define");
        internal static readonly Sym SetQ = New("set!");
        internal static readonly Sym Apply = New("apply");
        internal static readonly Sym CallCC = New("call/cc");
    }

    // ----------------------------------------------------------------------

    /// <summary>Linked list of bindings which map symbols to values</summary>
    public sealed class Environment: IEnumerable<Environment> {
        /// <summary>The bound symbol</summary>
        public readonly Sym Symbol;
        /// <summary>The value mapped from the bound symbol</summary>
        public object Value;
        /// <summary>The next binding</summary>
        public Environment Next;

        /// <summary>Construct a binding on the top of next.</summary>
        public Environment(Sym symbol, object value, Environment next) {
            Symbol = symbol;
            Value = value;
            Next = next;
        }

        /// <summary>Yield each binding.</summary>
        public IEnumerator<Environment> GetEnumerator() {
            var env = this;
            while (env != null) {
                yield return env;
                env = env.Next;
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        /// <summary>Search the bindings for a symbol.</summary>
        /// <exception cref="KeyNotFoundException">The symbol is not
        /// found in this environment.</exception>
        public Environment LookFor(Sym symbol) {
            foreach (var env in this)
                if (Object.ReferenceEquals(env.Symbol, symbol))
                    return env;
            throw new KeyNotFoundException(symbol.ToString());
        }

        /// <summary>Build a new environment by prepending the bindings of
        /// symbols and data to the present environment.</summary>
        public Environment PrependDefs(Cell symbols, Cell data) {
            if (symbols == null) {
                if (data != null)
                    throw new IndexOutOfRangeException
                        ($"surplus arg: ${LS.Stringify(data)}");
                return this;
            } else {
                if (data == null)
                    throw new IndexOutOfRangeException
                        ($"surplus param: ${LS.Stringify(symbols)}");
                return new Environment
                    ((Sym) symbols.Car, data.Car,
                     PrependDefs((Cell) symbols.Cdr, (Cell) data.Cdr));
            }
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>Operations in continuations</summary>
    internal enum ContOp {
        Then,
        Begin,
        Define,
        SetQ,
        Apply,
        ApplyFun,
        EvalArg,
        ConsArgs,
        RestoreEnv
    }

    /// <summary>Scheme's continuation as a stack of steps</summary>
    public sealed class Continuation {
        private Stack<(ContOp, object)> stack;

        /// <summary>Construct an empty continuation.</summary>
        public Continuation() {
            stack = new Stack<(ContOp, object)>();
        }

        /// <summary>Construct a copy of another continuation.</summary>
        public Continuation(Continuation other) {
            stack = new Stack<(ContOp, object)>(other.stack);
        }

        /// <summary>Copy steps from another continuation.</summary>
        public void CopyFrom(Continuation other) {
            stack = new Stack<(ContOp, object)>(other.stack);
        }

        /// <summary>Length of the continuation (an O(1) operation)</summary>
        public int Count => stack.Count;

        /// <summary>Return a quasi-stack trace.</summary>
        public override string ToString() {
            var ss = new List<string>();
            foreach (var (op, value) in stack)
                ss.Add($"{op} {LS.Stringify(value)}");
            return "#<" + String.Join("\n\t  ", ss) + ">";
        }

        /// <summary>Push a step to the top of the continuation.</summary>
        internal void Push(ContOp operation, object value) =>
            stack.Push((operation, value));

        /// <summary>Pop a step from the top of the continuation.</summary>
        internal (ContOp, object) Pop() => stack.Pop();

        /// <summary>Push ContOp.RestoreEnv unless on a tail call.</summary>
        internal void PushRestoreEnv(Environment env) {
            if (stack.TryPeek(out ValueTuple<ContOp, object> s)) {
                if (s.Item1 == ContOp.RestoreEnv)
                    return;     // tail call
            }
            Push(ContOp.RestoreEnv, env);
        }
    }

    // ----------------------------------------------------------------------

    /// <summary>Lambda expression with its environment</summary>
    public class Closure {
        /// <summary>A list of symbols as formal parameters</summary>
        public readonly Cell Params;
        /// <summary>A list of expressions as a body</summary>
        public readonly Cell Body;
        /// <summary>An environment of the expression</summary>
        public readonly Environment Env;

        /// <summary>Construct a new lambda expression.</summary>
        public Closure(Cell parameters, Cell body, Environment env) {
            Params = parameters;
            Body = body;
            Env = env;
        }
    }

    /// <summary>Built-in function</summary>
    public class Intrinsic {
        /// <summary>The function's name</summary>
        public readonly string Name;
        /// <summary>The function's arity, -1 if variadic</summary>
        public readonly int Arity;
        /// <summary>The function's body</summary>
        public readonly Func<Cell, object> Fun;

        /// <summary>Construct a new built-in function.</summary>
        public Intrinsic(string name, int arity, Func<Cell, object> fun) {
            Name = name;
            Arity = arity;
            Fun = fun;
        }

        /// <summary>Return a string which shows its name and arity.</summary>
        public override string ToString() => $"#<{Name}:{Arity}>";
    }

    // ----------------------------------------------------------------------

    /// <summary>Exception thrown by error procedure of SRFI-23</summary>
    public class ErrorException: Exception {
        /// <summary>Construct an exception with error arguments.</summary>
        public ErrorException(object reason, object arg):
            base($"Error: {LS.Stringify(reason, false)}: {LS.Stringify(arg)}")
            {}
    }

    // ----------------------------------------------------------------------

    /// <summary>Little Scheme's common values and functions</summary>
    public static class LS {
        /// <summary>A unique value which means the expression has no value
        /// </summary>
        public static readonly object None = new Object();

        /// <summary>A unique value which means the End Of File</summary>
        public static readonly object EOF = new Object();

        /// <summary>Convert an expression to a string.</summary>
        public static string Stringify(object exp, bool quote = true) {
            if (exp == null) {
                return "()";
            } else if (exp == None) {
                return "#<VOID>";
            } else if (exp == EOF) {
                return "#<EOF>";
            } else if (exp is bool b) {
                return b ? "#t" : "#f";
            } else if (exp is Cell j) {
                var ss = new List<string>();
                try {
                    foreach (object e in j)
                        ss.Add(Stringify(e, quote));
                } catch (ImproperListException ex) {
                    ss.Add(".");
                    ss.Add(Stringify(ex.Tail, quote));
                }
                return "(" + String.Join(" ", ss) + ")";
            } else if (exp is Environment env) {
                var ss = new List<string>();
                foreach (Environment e in env) {
                    if (e == GlobalEnv) {
                        ss.Add("GlobalEnv");
                        break;
                    } else if (e.Symbol == null) { // frame marker
                        ss.Add("|");
                    } else {
                        ss.Add(e.Symbol.ToString());
                    }
                }
                return "#<" + String.Join(" ", ss) + ">";
            } else if (exp is Closure cf) {
                return "#<" + Stringify(cf.Params) +
                    ":" + Stringify(cf.Body) +
                    ":" + Stringify(cf.Env) + ">";
            } else if (exp is string s && quote) {
                return "\"" + s + "\"";
            } else if (exp is double d) { // 123.0 => "123.0"
                string ds = d.ToString();
                unchecked {
                    if (((long) d).ToString() == ds)
                        ds += ".0";
                }
                return ds;
            }
            return $"{exp}";
        }

    // ----------------------------------------------------------------------

        private static Environment c(string name, int arity,
                                     Func<Cell, object> fun,
                                     Environment next) =>
            new Environment(Sym.New(name),
                            new Intrinsic(name, arity, fun), next);

        /// <summary>Return a list of symbols of the global environment.
        /// </summary>
        private static Cell Globals() {
            Cell j = null;
            Environment env = GlobalEnv.Next; // Skip the frame marker.
            foreach (var e in env)
                j = new Cell(e.Symbol, j);
            return j;
        }

        private static Environment G1 =
            c("+", 2, x => Arith.Add(x.Car, ((Cell) x.Cdr).Car),
              c("-", 2, x => Arith.Subtract(x.Car, ((Cell) x.Cdr).Car),
                c("*", 2, x => Arith.Multiply(x.Car, ((Cell) x.Cdr).Car),
                  c("<", 2, x => Arith.Compare(x.Car, ((Cell) x.Cdr).Car) < 0,
                    c("=", 2,
                      x => Arith.Compare(x.Car, ((Cell) x.Cdr).Car) == 0,
                      c("error", 2,
                        x => throw new ErrorException(x.Car,
                                                      ((Cell) x.Cdr).Car),
                        c("globals", 0, x => Globals(),
                          new Environment
                          (Sym.CallCC, Sym.CallCC,
                           new Environment
                           (Sym.Apply, Sym.Apply,
                            null)))))))));

        /// <summary>The global environment</summary>
        public static readonly Environment GlobalEnv = new Environment
            (null,              // frame marker
             null,
             c("car", 1, x => ((Cell) x.Car).Car,
               c("cdr", 1, x => ((Cell) x.Car).Cdr,
                 c("cons", 2, x => new Cell(x.Car, ((Cell) x.Cdr).Car),
                   c("eq?", 2, x =>
                     Object.ReferenceEquals(x.Car, ((Cell) x.Cdr).Car),
                     c("eqv?", 2,
                       x => {
                           object a = x.Car;
                           object b = ((Cell) x.Cdr).Car;
                           if (a == b) return true;
                           try {
                               return Arith.Compare(a, b) == 0;
                           } catch (ArgumentException) {
                               return false;
                           }
                       },
                       c("pair?", 1, x => x.Car is Cell,
                         c("null?", 1, x => x.Car == null,
                           c("not", 1, x => (x.Car is bool b && !b),
                             c("list", -1, x => x,
                               c("display", 1,
                                 x => {
                                     Console.Write(Stringify(x.Car, false));
                                     return None;
                                 },
                                 c("newline", 0,
                                   x => {
                                       Console.WriteLine();
                                       return None;
                                   },
                                   c("read", 0, x => ReadExpression("", ""),
                                     c("eof-object?", 1, x => x.Car == EOF,
                                       c("symbol?", 1, x => x.Car is Sym,
                                         G1)))))))))))))));

    // ----------------------------------------------------------------------

        /// <summary>Evaluate an expression in an environment.</summary>
        public static object Evaluate(object exp, Environment env) {
            var k = new Continuation();
            try {
                for (;;) {
                    evaluateExp:
                    for (;;) {
                        if (exp is Cell j) {
                            object kar = j.Car;
                            Cell kdr = (Cell) j.Cdr;
                            if (kar == Sym.Quote) { // (quote e)
                                exp = kdr.Car;
                                break;
                            } else if (kar == Sym.If) { // (if e1 e2 [e3])
                                exp = kdr.Car;
                                k.Push(ContOp.Then, kdr.Cdr);
                            } else if (kar == Sym.Begin) { // (begin e...)
                                exp = kdr.Car;
                                if (kdr.Cdr != null)
                                    k.Push(ContOp.Begin, kdr.Cdr);
                            } else if (kar == Sym.Lambda) {
                                // (lambda (v...) e...)
                                Cell parameters = (Cell) kdr.Car;
                                Cell body = (Cell) kdr.Cdr;
                                exp = new Closure(parameters, body, env);
                                break;
                            } else if (kar == Sym.Define) { // (define v e)
                                exp = ((Cell) kdr.Cdr).Car;
                                Sym v = (Sym) kdr.Car;
                                k.Push(ContOp.Define, v);
                            } else if (kar == Sym.SetQ) { // (set! v e)
                                exp = ((Cell) kdr.Cdr).Car;
                                Sym v = (Sym) kdr.Car;
                                k.Push(ContOp.SetQ, env.LookFor(v));
                            } else { // (fun arg...)
                                exp = kar;
                                k.Push(ContOp.Apply, kdr);
                            }
                        } else if (exp is Sym v) {
                            exp = env.LookFor(v).Value;
                            break;
                        } else { // a number, #t, #f etc.
                            break;
                        }
                    }
                    for (;;) {
                        // Console.Write("_{0}", k.Count);
                        if (k.Count == 0)
                            return exp;
                        var (op, x) = k.Pop();
                        switch (op) {
                        case ContOp.Then: { // x is (e2 [e3]).
                            Cell j = (Cell) x;
                            if (exp is bool b && !b) {
                                if (j.Cdr == null) {
                                    exp = None;
                                    break;
                                } else {
                                    exp = ((Cell) j.Cdr).Car; // e3
                                    goto evaluateExp;
                                }
                            } else {
                                exp = j.Car; // e2
                                goto evaluateExp;
                            }
                        }
                        case ContOp.Begin: { // x is (e...).
                            Cell j = (Cell) x;
                            if (j.Cdr != null) // Unless on a tail call..
                                k.Push(ContOp.Begin, j.Cdr);
                            exp = j.Car;
                            goto evaluateExp;
                        }
                        case ContOp.Define: // x is a variable name.
                            Debug.Assert(env.Symbol == null); // frame marker?
                            env.Next = new Environment((Sym) x, exp, env.Next);
                            exp = None;
                            break;
                        case ContOp.SetQ: // x is an Environment.
                            ((Environment) x).Value = exp;
                            exp = None;
                            break;
                        case ContOp.Apply:
                            // x is a list of args; exp is a function.
                            if (x == null) {
                                (exp, env) = ApplyFunction(exp, null, k, env);
                                break;
                            } else {
                                k.Push(ContOp.ApplyFun, exp);
                                Cell j = (Cell) x;
                                while (j.Cdr != null) {
                                    k.Push(ContOp.EvalArg, j.Car);
                                    j = (Cell) j.Cdr;
                                }
                                exp = j.Car;
                                k.Push(ContOp.ConsArgs, null);
                                goto evaluateExp;
                            }
                        case ContOp.ConsArgs: {
                            // x is a list of evaluated args (to be Cdr);
                            // exp is a newly evaluated arg (to be Car).
                            Cell args = new Cell(exp, x);
                            (op, exp) = k.Pop();
                            switch (op) {
                            case ContOp.EvalArg: // exp is the next arg.
                                k.Push(ContOp.ConsArgs, args);
                                goto evaluateExp;
                            case ContOp.ApplyFun: // exp is a function.
                                (exp, env) = ApplyFunction(exp, args, k, env);
                                break;
                            default:
                                throw new InvalidOperationException($"{op}");
                            }
                            break;
                        }
                        case ContOp.RestoreEnv: // x is an Environment.
                            env = (Environment) x;
                            break;
                        default:
                            throw new InvalidOperationException($"{op}");
                        }
                    }
                }
            } catch (ErrorException) {
                throw;
            } catch (Exception ex) {
                if (k.Count == 0) throw;
                throw new Exception(ex.GetType().Name + ": " + ex.Message +
                                    "\n\t" + Stringify(k), ex);
            }
        }

        /// <summary>Apply a function to arguments with a continuation
        /// and an environment.</summary>
        private static (object result, Environment env) ApplyFunction
        (object fun, Cell arg, Continuation k, Environment env) {
            for (;;) {
                if (fun == Sym.CallCC) {
                    k.PushRestoreEnv(env);
                    fun = arg.Car;
                    arg = new Cell(new Continuation(k), null);
                } else if (fun == Sym.Apply) {
                    fun = arg.Car;
                    arg = (Cell) ((Cell) arg.Cdr).Car;
                } else {
                    break;
                }
            }
            if (fun is Intrinsic bf) {
                if (bf.Arity >= 0)
                    if (arg == null ? bf.Arity > 0 : arg.Count() != bf.Arity)
                        throw new ArgumentException
                            ($"arity not matched: {bf} and {Stringify(arg)}");
                return (bf.Fun(arg), env);
            } else if (fun is Closure cf) {
                k.PushRestoreEnv(env);
                k.Push(ContOp.Begin, cf.Body);
                return (None,
                        new Environment(null, // frame marker
                                        null,
                                        cf.Env.PrependDefs(cf.Params, arg)));
            } else if (fun is Continuation cn) {
                k.CopyFrom(cn);
                return (arg.Car, env);
            } else {
                throw new ArgumentException
                    ("not a function: " + Stringify(fun) + " with " +
                     Stringify(arg));
            }
        }

    // ----------------------------------------------------------------------

        /// <summary>Split a string into an abstract sequence of tokens.
        /// </summary><remarks>For "(a 1)" it yields "(", "a", "1" and ")".
        /// </remarks>
        public static IEnumerable<string> SplitStringIntoTokens(string source) {
            var result = new Queue<string>();
            foreach (string line in source.Split('\n')) {
                var ss = new Queue<string>(); // to store string literals
                var x = new List<string>();
                int i = 0;
                foreach (string e in line.Split('"')) {
                    if (i % 2 == 0) {
                        x.Add(e);
                    } else {
                        ss.Enqueue("\"" + e); // Store a string literal.
                        x.Add("#s");
                    }
                    i++;
                }
                var s = String.Join(" ", x).Split(';')[0]; // Ignore "; ...".
                s = s.Replace("'", " ' ");
                s = s.Replace(")", " ) ");
                s = s.Replace("(", " ( ");
                foreach (string e in s.Split(' ', '\f', '\r', '\t', '\v')) {
                    if (e == "#s")
                        yield return ss.Dequeue();
                    else if (e != "")
                        yield return e;
                }
            }
        }

        /// <summary>Read an expression from tokens.</summary><remarks>
        /// Tokens will be left with the rest of the token strings, if any.
        /// </remarks>
        public static object ReadFromTokens(Queue<string> tokens) {
            string token = tokens.Dequeue();
            switch (token) {
            case "(": {
                Cell z = new Cell(null, null);
                Cell y = z;
                while (tokens.Peek() != ")") {
                    if (tokens.Peek() == ".") {
                        tokens.Dequeue();
                        y.Cdr = ReadFromTokens(tokens);
                        if (tokens.Peek() != ")")
                            throw new FormatException
                                ($") is expected: {tokens.Peek()}");
                        break;
                    }
                    var e = ReadFromTokens(tokens);
                    var x = new Cell(e, null);
                    y.Cdr = x;
                    y = x;
                }
                tokens.Dequeue();
                return z.Cdr;
            }
            case ")":
                throw new FormatException("unexpected )");
            case "'": {
                var e = ReadFromTokens(tokens);
                return new Cell(Sym.Quote, new Cell(e, null)); // (quote e)
            }
            case "#f":
                return false;
            case "#t":
                return true;
            }
            if (token[0] == '"')
                return token.Substring(1);
            if (Arith.TryParse(token, out object result))
                return result;
            return Sym.New(token);
        }

    // ----------------------------------------------------------------------

        /// <summary>Tokens from the standard-in</summary>
        private static Queue<string> StdInTokens = new Queue<string>();

        /// <summary>Read an expression from the console.</summary>
        public static object ReadExpression(string prompt1, string prompt2) {
            for (;;) {
                var old = new Queue<string>(StdInTokens);
                try {
                    return ReadFromTokens(StdInTokens);
                } catch (InvalidOperationException) { // The queue is empty.
                    Console.Write((old.Count == 0) ? prompt1 : prompt2);
                    Console.Out.Flush();
                    String line = Console.ReadLine();
                    if (line == null) // EOF
                        return EOF;
                    StdInTokens = old;
                    foreach (string token in SplitStringIntoTokens(line))
                        StdInTokens.Enqueue(token);
                } catch (Exception) {
                    StdInTokens.Clear(); // Discard erroneous tokens.
                    throw;
                }
            }
        }

        /// <summary>Repeat Read-Eval-Print until End-Of-File.</summary>
        public static void ReadEvalPrintLoop() {
            for (;;) {
                try {
                    object exp = ReadExpression("> ", "| ");
                    if (exp == EOF) {
                        Console.WriteLine("Goodbye");
                        return;
                    }
                    object result = Evaluate(exp, GlobalEnv);
                    if (result != None)
                        Console.WriteLine(Stringify(result));
                } catch (Exception ex) {
                    Console.WriteLine(ex.Message);
                }
            }
        }

        /// <summary>Load a source code from a file.</summary>
        public static void Load(string fileName) {
            String source = File.ReadAllText(fileName, Encoding.UTF8);
            var tokens = new Queue<string>(SplitStringIntoTokens(source));
            while (tokens.Count != 0) {
                object exp = ReadFromTokens(tokens);
                Evaluate(exp, GlobalEnv);
            }
        }
    }

    // ----------------------------------------------------------------------

    internal static class MainClass {
        private static int Main(string[] args) {
            try {
                if (args.Length > 0) {
                    LS.Load(args[0]);
                    if (args.Length < 2 || args[1] != "-")
                        return 0;
                }
                LS.ReadEvalPrintLoop();
                return 0;
            } catch (Exception ex) {
                Console.Error.WriteLine(ex.ToString());
                return 1;
            }
        }
    }
}
