using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace NDice
{
    public class DiceEngine : Expr.IVisitor<object>
    {
        public class InterpreterError : Exception
        {
            public InterpreterError(string message) : base(message)
            {

            }
        }

        public object Interpret(string expr)
        {
            return Interpret(expr, null);
        }

        public object Interpret(string expr, IDictionary<string, object> context)
        {
            return Interpret(new Parser(new Scanner(expr).ScanTokens()).Parse(), context);
        }

        private readonly Random _random = new Random();
        private IDictionary<string, object> _context;

        public object Interpret(Expr expr, IDictionary<string, object> context)
        {
            if (expr == null)
            {
                throw new InterpreterError("No expression to evaluate.");
            }

            _context = context;
            var result = Evaluate(expr);

            return result;
        }

        private object Evaluate(Expr expr)
        {
            return expr.Accept(this);
        }

        private void CheckNumberOperand(Token @operator, object operand)
        {
            if (operand is double) return;
            throw new InterpreterError("Operand must be a number");
        }

        private void CheckNumberOperand(Token @operator, object left, object right)
        {
            if (left is double && right is double) return;
            throw new InterpreterError("Operand must be a number");
        }

        public object VisitTertiaryExpr(Expr.Tertiary expr)
        {
            var condition = Evaluate(expr.Condition);

            if (IsTruthy(condition))
            {
                return Evaluate(expr.Then);
            }
            else
            {
                return Evaluate(expr.Else);
            }
        }

        public object VisitBinaryExpr(Expr.Binary expr)
        {
            var left = Evaluate(expr.Left);
            var right = Evaluate(expr.Right);

            switch (expr.Operator.Type)
            {
                case TokenType.Minus:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left - (double) right;
                case TokenType.Plus:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left + (double) right;
                case TokenType.Slash:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left / (double) right;
                case TokenType.Star:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left * (double) right;
                case TokenType.Greater:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left > (double) right;
                case TokenType.GreaterEqual:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left >= (double) right;
                case TokenType.Less:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left < (double) right;
                case TokenType.LessEqual:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left <= (double) right;
                case TokenType.BangEqual:
                    return !IsEqual(left, right);
                case TokenType.EqualEqual:
                    return IsEqual(left, right);
            }

            return null;
        }

        private bool IsEqual(object a, object b)
        {
            if (a == null && b == null) return true;
            if (a == null) return false;
            return a.Equals(b);
        }

        public object VisitUnaryExpr(Expr.Unary expr)
        {
            var right = Evaluate(expr.Right);

            switch (expr.Operator.Type)
            {
                case TokenType.Minus:
                    CheckNumberOperand(expr.Operator, right);
                    return -(double) right;
                case TokenType.Bang:
                    return !IsTruthy(right);
            }

            return null;
        }

        private bool IsTruthy(object obj)
        {
            if (obj == null) return false;
            if (obj is bool b) return b;
            return true;
        }

        public object VisitGroupingExpr(Expr.Grouping expr)
        {
            return Evaluate(expr.Expression);
        }

        public object VisitLiteralExpr(Expr.Literal expr)
        {
            if (expr.IsSubstitution)
            {
                if (!(expr.Value is string))
                {
                    throw new InterpreterError("Substitution must be a string.");
                }
                return _context[expr.Value.ToString()];
            }
            return expr.Value;
        }

        private int RollDie(int eyes)
        {
            return _random.Next(1, eyes);
        }

        public object VisitDiceExpr(Expr.Dice dice)
        {
            var _num = Evaluate(dice.Num);
            if (!(_num is double))
            {
                throw new InterpreterError("Dice number must be a number!");
            }
            var num = (int)Math.Round((double) _num);

            var _faces = Evaluate(dice.Faces);
            if (!(_faces is double))
            {
                throw new InterpreterError("Dice eyes must be a number!");
            }
            var faces = (int)Math.Round((double) _faces);

            if (dice.Mod != Expr.Dice.DiceMod.None)
            {
                var _modValue = Evaluate(dice.ModValue);
                if (!(_modValue is double))
                {
                    throw new InterpreterError("Dice mod value must be a number!");
                }

                var modValue = (int) Math.Round((double) _modValue);

                switch (dice.Mod)
                {
                    case Expr.Dice.DiceMod.CountSuccesses:
                        var successes = 0;
                        for (var i = 0; i < num; ++i)
                        {
                            var candidate = RollDie(faces);
                            switch (dice.ModOp)
                            {
                                case Expr.Dice.ModOperator.Equal:
                                    successes += candidate == modValue ? 1 : 0;
                                    break;
                                case Expr.Dice.ModOperator.Less:
                                    successes += candidate < modValue ? 1 : 0;
                                    break;
                                case Expr.Dice.ModOperator.LessEqual:
                                    successes += candidate <= modValue ? 1 : 0;
                                    break;
                                case Expr.Dice.ModOperator.Greater:
                                    successes += candidate > modValue ? 1 : 0;
                                    break;
                                case Expr.Dice.ModOperator.GreaterEqual:
                                    successes += candidate >= modValue ? 1 : 0;
                                    break;

                                default:
                                    throw new InterpreterError($"Unknown dice mod operator: '{dice.ModOp}'");
                            }
                        }
                        return (double) successes;
                    case Expr.Dice.DiceMod.MarginOfSuccess:
                        var result = 0;
                        for (var i = 0; i < num; ++i)
                        {
                            result += RollDie(faces);
                        }

                        switch (dice.ModOp)
                        {
                            case Expr.Dice.ModOperator.Equal:
                            case Expr.Dice.ModOperator.Greater:
                            case Expr.Dice.ModOperator.GreaterEqual:
                                result -= modValue;
                                break;
                            case Expr.Dice.ModOperator.Less:
                            case Expr.Dice.ModOperator.LessEqual:
                                result = modValue - result;
                                break;
                            default:
                                throw new InterpreterError($"Unknown dice mod operator: '{dice.ModOp}'");
                        }
                        return (double)result;

                }
            }

            var rollingSum = 0;
            for (var i = 0; i < num; ++i)
            {
                rollingSum += RollDie(faces);
            }

            return (double)rollingSum;
        }

        public object VisitCallExpr(Expr.Call expr)
        {
            var name = (expr.Callee as Expr.Literal)?.Value.ToString();

            if (name == null)
            {
                throw new InterpreterError("Function call without a name?!");
            }

            switch (name.ToLowerInvariant())
            {
                case "floor":
                    if (expr.Arguments.Count == 0 || expr.Arguments.Count > 1)
                    {
                        throw new InterpreterError($"floor takes exactly one argument, got {expr.Arguments.Count}.");
                    }
                    var fres = Evaluate(expr.Arguments.First());
                    if (!(fres is double))
                    {
                        throw new InterpreterError($"floor only takes numbers as arguments, got a '{fres.GetType()}'");
                    }

                    return Math.Floor((double)fres);
                case "ceil":
                case "ceiling":
                    if (expr.Arguments.Count == 0 || expr.Arguments.Count > 1)
                    {
                        throw new InterpreterError($"ceil takes exactly one argument, got {expr.Arguments.Count}.");
                    }
                    var cres = Evaluate(expr.Arguments.First());
                    if (!(cres is double))
                    {
                        throw new InterpreterError($"ceil only takes numbers as arguments, got a '{cres.GetType()}'");
                    }

                    return Math.Ceiling((double)cres);
                case "min":
                    if (expr.Arguments.Count == 0)
                    {
                        throw new InterpreterError($"min takes at least one argument, got 0.");
                    }

                    if (expr.Arguments.Count == 1)
                    {
                        return Evaluate(expr.Arguments.First());
                    }

                    var firstMin = Evaluate(expr.Arguments[0]);
                    CheckNumberOperand(null, firstMin);
                    var secondMin = Evaluate(expr.Arguments[1]);
                    CheckNumberOperand(null, secondMin);
                    var currentMin = Math.Min((double)firstMin, (double)secondMin);
                    for (var i = 2; i < expr.Arguments.Count; ++i)
                    {
                        var next = Evaluate(expr.Arguments[i]);
                        CheckNumberOperand(null, next);
                        currentMin = Math.Min(currentMin, (double) next);
                    }

                    return currentMin;
                case "max":
                    if (expr.Arguments.Count == 0)
                    {
                        throw new InterpreterError($"max takes at least one argument, got 0.");
                    }

                    if (expr.Arguments.Count == 1)
                    {
                        return Evaluate(expr.Arguments.First());
                    }

                    var firstMax = Evaluate(expr.Arguments[0]);
                    CheckNumberOperand(null, firstMax);
                    var secondMax = Evaluate(expr.Arguments[1]);
                    CheckNumberOperand(null, secondMax);
                    var currentMax = Math.Max((double)firstMax, (double)secondMax);
                    for (var i = 2; i < expr.Arguments.Count; ++i)
                    {
                        var next = Evaluate(expr.Arguments[i]);
                        CheckNumberOperand(null, next);
                        currentMax = Math.Max(currentMax, (double)next);
                    }

                    return currentMax;
                default:
                    throw new InterpreterError($"Unknown function '{name.ToLowerInvariant()}'");
            }
        }
    }
}
