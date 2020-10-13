using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NDice;
using NUnit.Framework;

namespace NDiceTests
{
    public class TokenizerData
    {
        public static IEnumerable Params
        {
            get
            {
                yield return new TestFixtureData("1(", 3, new Parser.ParserException(""), null, null);
                yield return new TestFixtureData("1d6d6", 6, new Parser.ParserException("Expected dice modifier, got 'Identifier d '."), null, null);

                yield return new TestFixtureData("1.5", 2, "1.5", 1.5, null);
                yield return new TestFixtureData("true", 2, "True", true, null);
                yield return new TestFixtureData("false", 2, "False", false, null);
                yield return new TestFixtureData("true == false", 4, "(== True False)", false, null);
                yield return new TestFixtureData("true == true", 4, "(== True True)", true, null);
                yield return new TestFixtureData("1 == 1", 4, "(== 1 1)", true, null);
                yield return new TestFixtureData("1 >= 1", 4, "(>= 1 1)", true, null);
                yield return new TestFixtureData("1 <= 1", 4, "(<= 1 1)", true, null);
                yield return new TestFixtureData("1 < 1", 4, "(< 1 1)", false, null);
                yield return new TestFixtureData("1 > 1", 4, "(> 1 1)", false, null);
                yield return new TestFixtureData("2d4 > 1", 6, "(> (d 2 4) 1)", true, null);

                yield return new TestFixtureData("floor(3/2)", 7, "(floor (/ 3 2))", 1, null);
                yield return new TestFixtureData("ceil(3/2)", 7, "(ceil (/ 3 2))", 2, null);
                yield return new TestFixtureData("min(3,2)", 7, "(min 3 2)", 2, null);
                yield return new TestFixtureData("max(3,2)", 7, "(max 3 2)", 3, null);

                yield return new TestFixtureData("1d6 > 3 ? 1 : 6", 10, "(?: (> (d 1 6) 3) 1 6)", new double[] { 1, 6 }, null);
                yield return new TestFixtureData("@test > 5", 4, "(> (@ test 3) 5)", false, new Dictionary<string, object>{{"test", 3.0d}});
                yield return new TestFixtureData("@test+5", 4, "(+ (@ test 3) 5)", 8, new Dictionary<string, object> { { "test", 3.0d } });
                yield return new TestFixtureData("@test.with.dots*12", 4, "(* (@ test.with.dots 3) 12)", 36, new Dictionary<string, object> { { "test.with.dots", 3.0d } });

                yield return new TestFixtureData("42", 2, "42", 42, null);
                yield return new TestFixtureData("42 + 21", 4, "(+ 42 21)", 63, null);
                yield return new TestFixtureData("42 - 21", 4, "(- 42 21)", 21, null);
                yield return new TestFixtureData("42 - 21 + 1", 6, "(+ (- 42 21) 1)", 22, null);
                yield return new TestFixtureData("1 + 42 - 21", 6, "(- (+ 1 42) 21)", 22, null);
                yield return new TestFixtureData("1 + (42 - 21)", 8, "(+ 1 (group (- 42 21)))", 22, null);
                yield return new TestFixtureData("(1 + 42) - 21", 8, "(- (group (+ 1 42)) 21)", 22, null);
                yield return new TestFixtureData("3 % 2", 4, "(% 3 2)", 1, null);
                yield return new TestFixtureData("3 % 3", 4, "(% 3 3)", 0, null);

                yield return new TestFixtureData("1d6", 4, "(d 1 6)", Enumerable.Range(1, 6).ToArray(), null);
                yield return new TestFixtureData("1d6 + 1d8", 8, "(+ (d 1 6) (d 1 8))", Enumerable.Range(2, 12).ToArray(), null);
                yield return new TestFixtureData("1d6 + 1d8 + 2d10", 12, "(+ (+ (d 1 6) (d 1 8)) (d 2 10))", Enumerable.Range(4, 30).ToArray(), null);
                yield return new TestFixtureData("1d20 + 5", 6, "(+ (d 1 20) 5)", Enumerable.Range(6, 20).ToArray(), null);
                yield return new TestFixtureData("1d8 + 5 * 2", 8, "(+ (d 1 8) (* 5 2))", Enumerable.Range(11, 8).ToArray(), null);
                yield return new TestFixtureData("(1d8 + 5) * 2", 10, "(* (group (+ (d 1 8) 5)) 2)", new double[] {12, 14, 16, 18, 20, 22, 24, 26}, null);
                yield return new TestFixtureData("(1d6) + (1d8)", 12, "(+ (group (d 1 6)) (group (d 1 8)))", Enumerable.Range(2, 12).ToArray(), null);
                yield return new TestFixtureData("(1d6)d6", 8, "(d (group (d 1 6)) 6)", Enumerable.Range(1, 36).ToArray(), null);
                yield return new TestFixtureData("1d6cs=1", 7, "(d 1 6 CountSuccesses Equal 1)", new double[] {0, 1}, null);
                yield return new TestFixtureData("1d6cs=1 + 2d4ms>(2d1)", 18, "(+ (d 1 6 CountSuccesses Equal 1) (d 2 4 MarginOfSuccess Greater (group (d 2 1))))", new double[] {0, 1, 2, 3, 4, 5, 6, 7}, null);

            }
        }
    }

    [TestFixtureSource(typeof(TokenizerData), "Params")]
    internal class TokenizerTests
    {
        private readonly string _expr;
        private readonly int _tokens;

        public TokenizerTests(string expr, int tokens, object _, object __, object ___)
        {
            _expr = expr;
            _tokens = tokens;
        }
        private static string TokensToString(IEnumerable<Token> tokens)
        {
            return tokens.Aggregate("", (current, token) => current + (token + " - "));
        }

        [Test]
        public void Tokenize()
        {
            var t = new Scanner(_expr).ScanTokens();
            Console.WriteLine(TokensToString(t));

            Assert.AreEqual(_tokens, t.Count);
        }
    }
}
