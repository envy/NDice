using System;
using System.Collections.Generic;
using NDice;
using NUnit.Framework;

namespace NDiceTests
{
    [TestFixtureSource(typeof(TokenizerData), "Params")]
    class ParserTests
    {
        private readonly string _expr;
        private readonly object _result;
        private readonly Dictionary<string, object> _context;
        public ParserTests(string expr, int _, object result, object __, Dictionary<string, object> context)
        {
            _expr = expr;
            _result = result;
            _context = context;
        }

        [Test]
        public void Parse()
        {
            if (_result.GetType() == typeof(Parser.ParserException))
            {
                Assert.Throws(_result.GetType(), () => new Parser(new Scanner(_expr).ScanTokens()).Parse());
            }
            else
            {
                var e = new Parser(new Scanner(_expr).ScanTokens()).Parse();
                var s = new AstPrinter().Print(e, _context);
                Console.WriteLine(s);
                Assert.AreEqual(_result, s);
            }
            
        }
    }
}
