// A little arithmetic in C# 7, R01.07.14 by SUZUKI Hisao
using System;
using System.Numerics;          // This implies /r:System.Numerics.dll

namespace LittleArith {
    /// <summary>Mixed mode arithmetic of int, double and BigInteger
    /// </summary><remarks>For values of other types, the methods of
    /// this class will throw ArgumentException.</remarks>
    public static class Arith {
        /// <summary>Convert a long into an int or a BigInteger.</summary>
        private static object Normalize(long x) {
            int i = (int) x;
            // NB: ((i == x) ? i : (BigInteger) x) will be a BigInteger.
            if (i == x) {
                return i;
            } else {
                return (BigInteger) x;
            }
        }

        /// <summary>Convert a BigInteger into an int if possible.</summary>
        private static object Normalize(BigInteger x) {
            try {
                return (int) x;
            } catch (OverflowException) {
                return x;
            }
        }

        /// <summary>x + y</summary>
        public static object Add(object x, object y) {
            switch (x) {
            case int a:
                switch (y) {
                case int b:
                    return Normalize((long) a + (long) b);
                case double b:
                    return a + b;
                case BigInteger b:
                    return Normalize(a + b);
                }
                break;
            case double a:
                switch (y) {
                case int b:
                    return a + b;
                case double b:
                    return a + b;
                case BigInteger b:
                    return a + (double) b;
                }
                break;
            case BigInteger a:
                switch (y) {
                case int b:
                    return Normalize(a + b);
                case double b:
                    return (double) a + b;
                case BigInteger b:
                    return Normalize(a + b);
                }
                break;
            }
            throw new ArgumentException($"{x}, {y}");
        }

        /// <summary>x - y</summary>
        public static object Subtract(object x, object y) {
            switch (x) {
            case int a:
                switch (y) {
                case int b:
                    return Normalize((long) a - (long) b);
                case double b:
                    return a - b;
                case BigInteger b:
                    return Normalize(a - b);
                }
                break;
            case double a:
                switch (y) {
                case int b:
                    return a - b;
                case double b:
                    return a - b;
                case BigInteger b:
                    return a - (double) b;
                }
                break;
            case BigInteger a:
                switch (y) {
                case int b:
                    return Normalize(a - b);
                case double b:
                    return (double) a - b;
                case BigInteger b:
                    return Normalize(a - b);
                }
                break;
            }
            throw new ArgumentException($"{x}, {y}");
        }

        /// <summary>x * y</summary>
        public static object Multiply(object x, object y) {
            switch (x) {
            case int a:
                switch (y) {
                case int b:
                    return Normalize((long) a * (long) b);
                case double b:
                    return a * b;
                case BigInteger b:
                    return Normalize(a * b);
                }
                break;
            case double a:
                switch (y) {
                case int b:
                    return a * b;
                case double b:
                    return a * b;
                case BigInteger b:
                    return a * (double) b;
                }
                break;
            case BigInteger a:
                switch (y) {
                case int b:
                    return Normalize(a * b);
                case double b:
                    return (double) a * b;
                case BigInteger b:
                    return Normalize(a * b);
                }
                break;
            }
            throw new ArgumentException($"{x}, {y}");
        }

        /// <summary>Compare x and y.</summary>
        /// <returns>-1, 0 or 1 as x is less than, equal to, or greater than y.
        /// </returns>
        public static int Compare(object x, object y) {
            switch (x) {
            case int a:
                switch (y) {
                case int b:
                    return Math.Sign((long) a - (long) b);
                case double b:
                    return Math.Sign(a - b);
                case BigInteger b:
                    return (a - b).Sign;
                }
                break;
            case double a:
                switch (y) {
                case int b:
                    return Math.Sign(a - b);
                case double b:
                    return Math.Sign(a - b);
                case BigInteger b:
                    return Math.Sign(a - (double) b);
                }
                break;
            case BigInteger a:
                switch (y) {
                case int b:
                    return (a - b).Sign;
                case double b:
                    return Math.Sign((double) a - b);
                case BigInteger b:
                    return (a - b).Sign;
                }
                break;
            }
            throw new ArgumentException($"{x}, {y}");
        }

        /// <summary>Try to parse a string as an int, a BigInteger or a double.
        /// </summary>
        /// <returns>true if s was parsed successfully; otherwise, false.
        /// </returns>
        public static bool TryParse(string s, out object result) {
            if (int.TryParse(s, out int i)) {
                result = i;
                return true;
            } else if (BigInteger.TryParse(s, out BigInteger b)) {
                result = b;
                return true;
            } else if (double.TryParse(s, out double d)) {
                result = d;
                return true;
            } else {
                result = double.NaN;
                return false;
            }
        }

#if TEST
        private static void Main() {
            object x = Normalize(3L);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 3, System.Int32
            x = Normalize(555_000_555_000);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 555000555000, System.Numerics.BigInteger

            x = Normalize((BigInteger) 3);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 3, System.Int32
            x = Normalize((BigInteger) 555_000_555_000);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 555000555000, System.Numerics.BigInteger

            try {
                x = Add("123", 4);
            } catch (ArgumentException ex) {
                Console.WriteLine(ex.Message); // -> 123, 4
            }
            x = Add(2, 7.89);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 9.89, System.Double
            x = Add(2, (BigInteger) 12345678901234567890);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 12345678901234567892, Sysmtem.Numerics.BigInteger
            x = Add((BigInteger) 12345678901234567890, 1.0);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 1.23456789012346E+19, Sysmtem.Double
            x = Add(1_000_111_000, 2_000_222_000);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 3000333000, Sysmtem.Numerics.BigInteger
            x = Add((BigInteger) 3_000_333_000, (BigInteger)(-2_000_222_000));
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 1000111000, Sysmtem.Int32

            x = Subtract((BigInteger)3_000_333_000, (BigInteger)2_000_222_000);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 1000111000, Sysmtem.Int32

            x = Multiply(2.2, (BigInteger) 3);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 6.6, Sysmtem.Double

            Console.WriteLine("{0}", Compare(2.2, (BigInteger) 3));
            // -> -1

            bool b = TryParse("123", out x);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 123, Sysmtem.Int32
            b = TryParse("123.4", out x);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> 123.4, Sysmtem.Double
            b = TryParse("-12345678901234567890", out x);
            Console.WriteLine("{0}, {1}", x, x.GetType());
            // -> -12345678901234567890, Sysmtem.Numerics.BigInteger
        }
#endif
    }
}