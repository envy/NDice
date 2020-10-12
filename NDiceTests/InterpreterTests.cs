using System;
using System.Collections;
using System.Collections.Generic;
using NDice;
using NUnit.Framework;

namespace NDiceTests
{
    [TestFixtureSource(typeof(TokenizerData), "Params")]
    class InterpreterTests
    {
        private readonly string _expr;
        private readonly object _exc;
        private readonly object _result;
        private readonly Dictionary<string, object> _context;
        public InterpreterTests(string expr, int _, object exc, object result, Dictionary<string, object> context)
        {
            _expr = expr;
            _result = result;
            _exc = exc;
            _context = context;
        }

        [Test]
        public void Interpret()
        {
            if (_exc.GetType() == typeof(Parser.ParserException))
            {
                Assert.Throws(_exc.GetType(), () => new Parser(new Scanner(_expr).ScanTokens()).Parse());
            }
            else
            {
                var r = new DiceEngine().Interpret(new Parser(new Scanner(_expr).ScanTokens()).Parse(), _context);
                Console.WriteLine(r);
                if (_result is ICollection result)
                {
                    Assert.Contains(r, result);
                }
                else
                {
                    Assert.AreEqual(_result, r);
                }
            }
        }
    }
}
