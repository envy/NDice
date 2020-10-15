using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;

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

        private string DiceModToString(Expr.Dice.DiceMod mod)
        {
            switch (mod)
            {
                case Expr.Dice.DiceMod.None:
                    return "";
                case Expr.Dice.DiceMod.KeepHighest:
                    return "kh";
                case Expr.Dice.DiceMod.KeepLowest:
                    return "kl";
                case Expr.Dice.DiceMod.DropHighest:
                    return "dh";
                case Expr.Dice.DiceMod.DropLowest:
                    return "dl";
                case Expr.Dice.DiceMod.CountSuccesses:
                    return "cs";
                case Expr.Dice.DiceMod.MarginOfSuccess:
                    return "ms";
                case Expr.Dice.DiceMod.Explode:
                    return "x";
                case Expr.Dice.DiceMod.CompoundExplode:
                    return "cx";
                case Expr.Dice.DiceMod.ReRoll:
                    return "r";
                default:
                    return "??";
            }
        }

        private string DiceModOpToString(Expr.Dice.ModOperator modOp)
        {
            switch (modOp)
            {
                case Expr.Dice.ModOperator.Equal:
                    return "=";
                case Expr.Dice.ModOperator.Less:
                    return "<";
                case Expr.Dice.ModOperator.LessEqual:
                    return "<=";
                case Expr.Dice.ModOperator.Greater:
                    return ">=";
                case Expr.Dice.ModOperator.GreaterEqual:
                    return ">=";
                default:
                    return "??";
            }
        }

        public string VisitDiceExpr(Expr.Dice dice)
        {
            var s = dice.Results.Aggregate("(", (s1, r) => s1 + (s1 == "(" ? $"{r}" : $" + {r}"));
            s += ")";
            if (dice.Mod != Expr.Dice.DiceMod.None)
            {
                s += $"{DiceModToString(dice.Mod)}{DiceModOpToString(dice.ModOp)}{dice.ModValue.Accept(this)}";
            }
            return s;
        }

        private string DicePoolModToString(Expr.DicePool.DicePoolMod mod)
        {
            switch (mod)
            {
                case Expr.DicePool.DicePoolMod.KeepHighest:
                    return "kh";
                case Expr.DicePool.DicePoolMod.KeepLowest:
                    return "kl";
                case Expr.DicePool.DicePoolMod.DropHighest:
                    return "dh";
                case Expr.DicePool.DicePoolMod.DropLowest:
                    return "dl";
                case Expr.DicePool.DicePoolMod.CountSuccesses:
                    return "cs";
                default:
                    return "??";
            }
        }

        private string DicePoolModOpToString(Expr.DicePool.ModOperator modOp)
        {
            switch (modOp)
            {
                case Expr.DicePool.ModOperator.Equal:
                    return "=";
                case Expr.DicePool.ModOperator.Less:
                    return "<";
                case Expr.DicePool.ModOperator.LessEqual:
                    return "<=";
                case Expr.DicePool.ModOperator.Greater:
                    return ">";
                case Expr.DicePool.ModOperator.GreaterEqual:
                    return ">=";
                default:
                    return "??";
            }
        }

        public string VisitDicePoolExpr(Expr.DicePool expr)
        {
            return $"{{{expr.Arguments.Aggregate("", (s, expr1) => s + (string.IsNullOrEmpty(s) ? expr1.Accept(this) : $", {expr1.Accept(this)}"))}}}{DicePoolModToString(expr.Mod)}{DicePoolModOpToString(expr.ModOp)}{expr.ModValue.Accept(this)}";
        }

        public string VisitCallExpr(Expr.Call expr)
        {
            return $"{expr.Callee.Accept(this)}({expr.Arguments.Aggregate("", (s, expr1) => s + (string.IsNullOrEmpty(s) ? expr1.Accept(this) : $", {expr1.Accept(this)}") )})";
        }
    }
}
