using System;
using NDice;
using Newtonsoft.Json;

namespace NDiceInterop
{
    public class Wasm
    {
        internal struct InterpretResult
        {
            public string Result { get; set; }
            public string Ast { get; set; }
            public string Pretty { get; set; }
            public string Error { get; set; }
        }

        public static string Interpret(string term)
        {
            var interpretResult = new InterpretResult();
            try
            {
                var expr = new Parser(new Scanner(term).ScanTokens()).Parse();
                var result = new DiceEngine().Interpret(expr).ToString();

                interpretResult.Error = null;
                interpretResult.Ast = new AstPrinter().Print(expr, null);
                interpretResult.Pretty = new PrettyPrinter().PrettyPrint(expr, null);
                interpretResult.Result = result;
            }
            catch (Exception e)
            {
                interpretResult.Error = e.Message;
            }

            return JsonConvert.SerializeObject(interpretResult);
        }
    }
}
