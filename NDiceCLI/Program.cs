using System;
using System.Collections.Generic;
using System.Linq;
using NDice;

namespace NDiceCLI
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("NDiceCLI - let's roll!");

            var de = new DiceEngine();

            while (true)
            {
                Console.Write("> ");

                var input = Console.ReadLine()?.Trim();
                if (string.IsNullOrEmpty(input))
                {
                    continue;
                }

                if (input.ToLowerInvariant() == "quit" || input.ToLowerInvariant() == "exit")
                {
                    return;
                }

                try
                {
                    var tokens = new Scanner(input).ScanTokens();
                    Console.WriteLine($"TKN: {TokensToString(tokens)}");
                    var expr = new Parser(tokens).Parse();
                    Console.WriteLine($"AST: {new AstPrinter().Print(expr, null)}");
                    Console.WriteLine($"RES: {de.Interpret(expr, null)}");
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            }
        }

        private static string TokensToString(IEnumerable<Token> tokens)
        {
            return tokens.Aggregate("", (current, token) => current + (token + " - "));
        }
    }
}
