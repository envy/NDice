using System;
using System.Collections.Generic;
using System.Linq;

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

        public object Interpret(Expr expr)
        {
            return Interpret(expr, null);
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
            throw new InterpreterError($"{@operator}: Operand must be a number");
        }

        private void CheckNumberOperand(Token @operator, object left, object right)
        {
            if (left is double && right is double) return;
            throw new InterpreterError($"{@operator}: Operand must be a number");
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
                case TokenType.Percent:
                    CheckNumberOperand(expr.Operator, left, right);
                    return (double) left % (double) right;
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
            if (a is double da && b is double db)
                return Math.Abs(da - db) < 0.00001;
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

                if (_context == null || !(_context.ContainsKey(expr.Value.ToString())))
                {
                    throw new InterpreterError($"Context does not contain key '{expr.Value}'");
                }

                expr.Value = _context[expr.Value.ToString()];
                expr.IsSubstitution = false;
            }
            return expr.Value;
        }

        private int RollDie(int faces)
        {
            return _random.Next(1, faces + 1);
        }

        public object VisitDiceExpr(Expr.Dice dice)
        {
            if (dice.Results.Count > 0)
            {
                // dice were already rolled.
                return dice.Result;
            }

            var oNum = Evaluate(dice.Num);
            if (!(oNum is double))
            {
                throw new InterpreterError($"Dice number must be a number but is a {oNum.GetType()}!");
            }
            var num = (int)Math.Round((double) oNum);

            var oFaces = Evaluate(dice.Faces);
            if (!(oFaces is double))
            {
                throw new InterpreterError($"Dice faces must be a number but is a {oFaces.GetType()}!");
            }
            var faces = (int)Math.Round((double) oFaces);

            if (dice.Mod != Expr.Dice.DiceMod.None)
            {
                var oModValue = Evaluate(dice.ModValue);
                if (!(oModValue is double))
                {
                    throw new InterpreterError($"Dice mod value must be a number but is a {oModValue.GetType()}!");
                }

                var modValue = (double) oModValue;

                switch (dice.Mod)
                {
                    case Expr.Dice.DiceMod.CountSuccesses:
                    {
                        for (var i = 0; i < num; ++i)
                        {
                            dice.Results.Add(RollDie(faces));
                        }

                        switch (dice.ModOp)
                        {
                            case Expr.Dice.ModOperator.Equal:
                                return dice.Result = dice.Results.Count(d => Math.Abs(d - modValue) < 0.00001);
                            case Expr.Dice.ModOperator.Less:
                                return dice.Result = dice.Results.Count(d => d < modValue);
                            case Expr.Dice.ModOperator.LessEqual:
                                return dice.Result =  dice.Results.Count(d => d <= modValue);
                            case Expr.Dice.ModOperator.Greater:
                                return dice.Result =  dice.Results.Count(d => d > modValue);
                            case Expr.Dice.ModOperator.GreaterEqual:
                                return dice.Result = dice.Results.Count(d => d >= modValue);
                            default:
                                throw new InterpreterError($"Unknown dice mod operator: '{dice.ModOp}'");
                        }
                    }
                    case Expr.Dice.DiceMod.MarginOfSuccess:
                    {
                        var result = 0.0d;
                        for (var i = 0; i < num; ++i)
                        {
                            var tmp = RollDie(faces);
                            dice.Results.Add(tmp);
                            result += tmp;
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

                        return dice.Result = result;
                    }
                    case Expr.Dice.DiceMod.ReRoll:
                    {
                        var result = 0.0d;
                        for (var i = 0; i < num; ++i)
                        {
                            var tmp = RollDie(faces);
                            switch (dice.ModOp)
                            { 
                                case Expr.Dice.ModOperator.Equal:
                                    while (Math.Abs(tmp - modValue) < 0.00001)
                                    {
                                        tmp = RollDie(faces);
                                    }
                                    break;
                                case Expr.Dice.ModOperator.Greater:
                                    while (tmp > modValue)
                                    {
                                        tmp = RollDie(faces);
                                    }
                                    break;
                                case Expr.Dice.ModOperator.GreaterEqual:
                                    while (tmp >= modValue)
                                    {
                                        tmp = RollDie(faces);
                                    }
                                    break;
                                case Expr.Dice.ModOperator.Less:
                                    while (tmp < modValue)
                                    {
                                        tmp = RollDie(faces);
                                    }
                                    break;
                                case Expr.Dice.ModOperator.LessEqual:
                                    while (tmp <= modValue)
                                    {
                                        tmp = RollDie(faces);
                                    }
                                    break;
                            }

                            dice.Results.Add(tmp);
                            result += tmp;
                        }

                        return dice.Result = result;
                    }
                    case Expr.Dice.DiceMod.Explode:
                    {
                        var result = 0.0d;
                        for (var i = 0; i < num; ++i)
                        {
                            var tmp = RollDie(faces);
                            switch (dice.ModOp)
                            {
                                case Expr.Dice.ModOperator.Equal:
                                    if (Math.Abs(tmp - modValue) < 0.00001)
                                    {
                                        num++;
                                    }

                                    break;
                                case Expr.Dice.ModOperator.Greater:
                                    if (tmp > modValue)
                                    {
                                        num++;
                                    }

                                    break;
                                case Expr.Dice.ModOperator.GreaterEqual:
                                    if (tmp >= modValue)
                                    {
                                        num++;
                                    }

                                    break;
                                case Expr.Dice.ModOperator.Less:
                                    if (tmp < modValue)
                                    {
                                        num++;
                                    }

                                    break;
                                case Expr.Dice.ModOperator.LessEqual:
                                    if (tmp <= modValue)
                                    {
                                        num++;
                                    }

                                    break;
                            }

                            dice.Results.Add(tmp);
                            result += tmp;
                        }

                        return dice.Result = result;
                    }
                    case Expr.Dice.DiceMod.CompoundExplode:
                    {
                        var result = 0.0d;
                        for (var i = 0; i < num; ++i)
                        {
                            var compundSum = 0.0d;
                            var tmp = RollDie(faces);
                            compundSum += tmp;
                            switch (dice.ModOp)
                            {
                                case Expr.Dice.ModOperator.Equal:
                                    while (Math.Abs(tmp - modValue) < 0.00001)
                                    {
                                        tmp = RollDie(faces);
                                        compundSum += tmp;
                                    }
                                    break;
                                case Expr.Dice.ModOperator.Greater:
                                    while (tmp > modValue)
                                    {
                                        tmp = RollDie(faces);
                                        compundSum += tmp;
                                    }
                                    break;
                                case Expr.Dice.ModOperator.GreaterEqual:
                                    while (tmp >= modValue)
                                    {
                                        tmp = RollDie(faces);
                                        compundSum += tmp;
                                    }
                                    break;
                                case Expr.Dice.ModOperator.Less:
                                    while (tmp < modValue)
                                    {
                                        tmp = RollDie(faces);
                                        compundSum += tmp;
                                    }
                                    break;
                                case Expr.Dice.ModOperator.LessEqual:
                                    while (tmp <= modValue)
                                    {
                                        tmp = RollDie(faces);
                                        compundSum += tmp;
                                    }
                                    break;
                            }

                            dice.Results.Add(compundSum);
                            result += compundSum;
                        }

                        return dice.Result = result;
                    }
                    case Expr.Dice.DiceMod.KeepHighest:
                    {
                        for (var i = 0; i < num; ++i)
                        {
                            dice.Results.Add(RollDie(faces));
                        }
                        return dice.Result = dice.Results.OrderByDescending(d => d).Take(Convert.ToInt32(modValue)).Sum();
                    }
                    case Expr.Dice.DiceMod.KeepLowest:
                    {
                        for (var i = 0; i < num; ++i)
                        {
                            dice.Results.Add(RollDie(faces));
                        }
                        return dice.Result = dice.Results.OrderBy(d => d).Take(Convert.ToInt32(modValue)).Sum();
                    }
                    case Expr.Dice.DiceMod.DropHighest:
                    {
                        for (var i = 0; i < num; ++i)
                        {
                            dice.Results.Add(RollDie(faces));
                        }
                        return dice.Result = dice.Results.OrderByDescending(d => d).Skip(Convert.ToInt32(modValue)).Sum();
                    }
                    case Expr.Dice.DiceMod.DropLowest:
                    {
                        for (var i = 0; i < num; ++i)
                        {
                            dice.Results.Add(RollDie(faces));
                        }
                        return dice.Result = dice.Results.OrderBy(d => d).Skip(Convert.ToInt32(modValue)).Sum();
                    }
                    default:
                        throw new InterpreterError($"Unknown dice modifier '{dice.Mod}'");
                }
            }

            var rollingSum = 0;
            for (var i = 0; i < num; ++i)
            {
                var tmp = RollDie(faces);
                dice.Results.Add(tmp);
                rollingSum += tmp;
            }

            return dice.Result = rollingSum;
        }

        public object VisitDicePoolExpr(Expr.DicePool expr)
        {
            switch (expr.Mod)
            {
                case Expr.DicePool.DicePoolMod.KeepHighest:
                {
                    var results = new List<double>();
                    foreach (var argument in expr.Arguments)
                    {
                        var result = argument.Accept(this);
                        CheckNumberOperand(null, result);
                        results.Add((double) result);
                    }

                    var modValue = expr.ModValue.Accept(this);
                    CheckNumberOperand(null, modValue);

                    results.Sort();
                    results.Reverse();
                    return results.Take(Convert.ToInt32(modValue)).Sum();
                }
                case Expr.DicePool.DicePoolMod.KeepLowest:
                {
                    var results = new List<double>();
                    foreach (var argument in expr.Arguments)
                    {
                        var result = argument.Accept(this);
                        CheckNumberOperand(null, result);
                        results.Add((double)result);
                    }

                    var modValue = expr.ModValue.Accept(this);
                    CheckNumberOperand(null, modValue);

                    results.Sort();
                    return results.Take(Convert.ToInt32(modValue)).Sum();
                }
                case Expr.DicePool.DicePoolMod.DropHighest:
                {
                    var results = new List<double>();
                    foreach (var argument in expr.Arguments)
                    {
                        var result = argument.Accept(this);
                        CheckNumberOperand(null, result);
                        results.Add((double)result);
                    }

                    var modValue = expr.ModValue.Accept(this);
                    CheckNumberOperand(null, modValue);

                    results.Sort();
                    results.Reverse();
                    return results.Skip(Convert.ToInt32(modValue)).Sum();
                }
                case Expr.DicePool.DicePoolMod.DropLowest:
                {
                    var results = new List<double>();
                    foreach (var argument in expr.Arguments)
                    {
                        var result = argument.Accept(this);
                        CheckNumberOperand(null, result);
                        results.Add((double)result);
                    }

                    var modValue = expr.ModValue.Accept(this);
                    CheckNumberOperand(null, modValue);

                    results.Sort();
                    return results.Skip(Convert.ToInt32(modValue)).Sum();
                }
                case Expr.DicePool.DicePoolMod.CountSuccesses:
                {
                    var results = new List<double>();
                    foreach (var argument in expr.Arguments)
                    {
                        var result = argument.Accept(this);
                        CheckNumberOperand(null, result);
                        results.Add((double)result);
                    }

                    var modValue = expr.ModValue.Accept(this);
                    CheckNumberOperand(null, modValue);
                    switch (expr.ModOp)
                    {
                        case Expr.DicePool.ModOperator.Equal:
                            return (double) results.Count(d => Math.Abs(d - (double)modValue) < 0.00001);
                        case Expr.DicePool.ModOperator.Less:
                            return (double) results.Count(d => d < (double) modValue);
                        case Expr.DicePool.ModOperator.LessEqual:
                            return (double) results.Count(d => d <= (double)modValue);
                        case Expr.DicePool.ModOperator.Greater:
                            return (double) results.Count(d => d > (double)modValue);
                        case Expr.DicePool.ModOperator.GreaterEqual:
                            return (double) results.Count(d => d >= (double)modValue);
                        default:
                            throw new InterpreterError($"Invalid dice pool modifier operator '{expr.ModOp}' for '{expr.Mod}'");
                    }
                }
                default:
                    throw new InterpreterError($"Unknown dice pool modifier '{expr.Mod}'");
            }
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
                    return Floor(expr);
                case "ceil":
                case "ceiling":
                    return Ceil(expr);
                case "round":
                    return Round(expr);
                case "min":
                    return Min(expr);
                case "max":
                    return Max(expr);
                default:
                    throw new InterpreterError($"Unknown function '{name.ToLowerInvariant()}'");
            }
        }

        private double Floor(Expr.Call expr)
        {
            if (expr.Arguments.Count == 0 || expr.Arguments.Count > 1)
            {
                throw new InterpreterError($"floor takes exactly one argument, got {expr.Arguments.Count}.");
            }
            var res = Evaluate(expr.Arguments.First());
            CheckNumberOperand(expr.Function, res);

            return Math.Floor((double)res);
        }

        private double Ceil(Expr.Call expr)
        {
            if (expr.Arguments.Count == 0 || expr.Arguments.Count > 1)
            {
                throw new InterpreterError($"ceil takes exactly one argument, got {expr.Arguments.Count}.");
            }
            var res = Evaluate(expr.Arguments.First());
            CheckNumberOperand(expr.Function, res);

            return Math.Ceiling((double)res);
        }

        private double Round(Expr.Call expr)
        {
            if (expr.Arguments.Count == 0 || expr.Arguments.Count > 1)
            {
                throw new InterpreterError($"round takes exactly one argument, got {expr.Arguments.Count}.");
            }
            var res = Evaluate(expr.Arguments.First());
            CheckNumberOperand(expr.Function, res);

            return Math.Round((double)res);
        }

        private double Min(Expr.Call expr)
        {
            if (expr.Arguments.Count == 0)
            {
                throw new InterpreterError($"min takes at least one argument, got 0.");
            }

            if (expr.Arguments.Count == 1)
            {
                var res = Evaluate(expr.Arguments.First());
                CheckNumberOperand(expr.Function, res);
                return (double) res;
            }

            var firstMin = Evaluate(expr.Arguments[0]);
            CheckNumberOperand(expr.Function, firstMin);
            var secondMin = Evaluate(expr.Arguments[1]);
            CheckNumberOperand(expr.Function, secondMin);
            var currentMin = Math.Min((double)firstMin, (double)secondMin);
            for (var i = 2; i < expr.Arguments.Count; ++i)
            {
                var next = Evaluate(expr.Arguments[i]);
                CheckNumberOperand(expr.Function, next);
                currentMin = Math.Min(currentMin, (double)next);
            }

            return currentMin;
        }

        private double Max(Expr.Call expr)
        {
            if (expr.Arguments.Count == 0)
            {
                throw new InterpreterError($"max takes at least one argument, got 0.");
            }

            if (expr.Arguments.Count == 1)
            {
                var res = Evaluate(expr.Arguments.First());
                CheckNumberOperand(expr.Function, res);
                return (double)res;
            }

            var firstMax = Evaluate(expr.Arguments[0]);
            CheckNumberOperand(expr.Function, firstMax);
            var secondMax = Evaluate(expr.Arguments[1]);
            CheckNumberOperand(expr.Function, secondMax);
            var currentMax = Math.Max((double)firstMax, (double)secondMax);
            for (var i = 2; i < expr.Arguments.Count; ++i)
            {
                var next = Evaluate(expr.Arguments[i]);
                CheckNumberOperand(expr.Function, next);
                currentMax = Math.Max(currentMax, (double)next);
            }

            return currentMax;
        }
    }
}
