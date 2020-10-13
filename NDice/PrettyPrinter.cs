using System.Collections.Generic;
using System.Linq;

namespace NDice
{
    public class PrettyPrinter : Expr.IVisitor<string>
    {
        public PrettyPrinter()
        {

        }

        private IDictionary<string, object> _context;
        public string PrettyPrint(Expr expr, IDictionary<string, object> context)
        {
            _context = context;

            return expr.Accept(this);
        }

        public string VisitTertiaryExpr(Expr.Tertiary expr)
        {
            return $"{expr.Condition.Accept(this)} ? {expr.Then.Accept(this)} : {expr.Else.Accept(this)}";
        }

        public string VisitBinaryExpr(Expr.Binary expr)
        {
            var left = expr.Left.Accept(this);
            var right = expr.Right.Accept(this);

            return $"{left} {expr.Operator.Lexeme} {right}";
        }

        public string VisitUnaryExpr(Expr.Unary expr)
        {
            return $"{expr.Operator}{expr.Right.Accept(this)}";
        }

        public string VisitGroupingExpr(Expr.Grouping expr)
        {
            return $"({expr.Expression.Accept(this)})";
        }

        public string VisitLiteralExpr(Expr.Literal expr)
        {
            return expr.Value.ToString();
        }

        public string VisitDiceExpr(Expr.Dice dice)
        {
            var s = dice.Results.Aggregate("(", (s1, r) => s1 + (s1 == "(" ? $"{r}" : $" + {r}"));
            s += ")";
            if (dice.Mod != Expr.Dice.DiceMod.None)
            {

            }
            return s;
        }

        public string VisitCallExpr(Expr.Call expr)
        {
            return $"{expr.Callee.Accept(this)}({expr.Arguments.Aggregate("", (s, expr1) => s + (string.IsNullOrEmpty(s) ? expr1.Accept(this) : $", {expr1.Accept(this)}") )})";
        }
    }
}
