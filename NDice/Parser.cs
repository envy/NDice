using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Linq.Expressions;

namespace NDice
{
    public class Parser
    {
        public class ParserException : Exception
        {
            public ParserException(string message) : base(message)
            {

            }
        }

        private readonly IList<Token> _tokens;
        private int _current;

        public Parser(IList<Token> tokens)
        {
            _tokens = tokens;
            _current = 0;
        }

        public Expr Parse()
        {
            var expr = Expression();
            if (!IsAtEnd())
            {
                throw new ParserException("Excess characters after parsing finished.");
            }

            return expr;
        }

        private bool Match(params TokenType[] types)
        {
            foreach (var type in types)
            {
                if (Check(type))
                {
                    Advance();
                    return true;
                }
            }

            return false;
        }

        private Token Consume(TokenType type, string message)
        {
            if (Check(type)) return Advance();

            throw new ParserException(message);
        }

        private bool Check(TokenType type)
        {
            if (IsAtEnd()) return false;
            return Peek().Type == type;
        }

        private Token Advance()
        {
            if (!IsAtEnd()) _current++;
            return Previous();
        }

        private bool IsAtEnd()
        {
            return Peek().Type == TokenType.Eof;
        }

        private Token Peek()
        {
            return _tokens[_current];
        }

        private Token Previous()
        {
            return _tokens[_current - 1];
        }

        private Expr Expression()
        {
            Expr expr = Equality();

            if (Match(TokenType.IfThen))
            {
                Expr then = Expression();
                Consume(TokenType.Else, "Expected ':' after then-branch of ?.");
                Expr @else = Expression();
                
                expr = new Expr.Tertiary(expr, then, @else);
            }

            return expr;
        }

        private Expr Equality()
        {
            Expr expr = Comparison();

            while (Match(TokenType.BangEqual, TokenType.EqualEqual))
            {
                Token @operator = Previous();
                Expr right = Comparison();
                expr = new Expr.Binary(expr, @operator, right);
            }

            return expr;
        }

        private Expr Comparison()
        {
            Expr expr = Term();

            while (Match(TokenType.Greater, TokenType.GreaterEqual, TokenType.Less, TokenType.LessEqual))
            {
                Token @operator = Previous();
                Expr right = Term();
                expr = new Expr.Binary(expr, @operator, right);
            }

            return expr;
        }

        private Expr Term()
        {
            Expr expr = Factor();

            while (Match(TokenType.Minus, TokenType.Plus))
            {
                Token @operator = Previous();
                Expr right = Factor();
                expr = new Expr.Binary(expr, @operator, right);
            }

            return expr;
        }

        private Expr Factor()
        {
            Expr expr = Dice();

            while (Match(TokenType.Slash, TokenType.Star, TokenType.Percent))
            {
                Token @operator = Previous();
                Expr right = Dice();
                expr = new Expr.Binary(expr, @operator, right);
            }

            return expr;
        }

        private Expr Dice()
        {
            Expr expr = Unary();

            // Match dice
            while (Match(TokenType.Identifier))
            {
                Token id = Previous();
                if ("d".Equals(id.Lexeme) || "D".Equals(id.Lexeme))
                {
                    Expr faces = Call(false);

                    // Check for modifiers
                    if (Match(TokenType.Identifier, TokenType.Bang, TokenType.BangBang))
                    {
                        GetDiceModifiers(faces, out var mod, out var modOp, out var modValue);
                        expr = new Expr.Dice(expr, faces, GetLabel(), mod, modOp, modValue);
                    }
                    else
                    {
                        expr = new Expr.Dice(expr, faces, GetLabel());
                    }
                }
            }

            return expr;
        }

        private string GetLabel()
        {
            if (!Check(TokenType.LeftBracket)) return null;
            Advance();
            var label = "";
            while (Check(TokenType.Identifier))
            {
                label = string.IsNullOrEmpty(label) ? Advance().Lexeme : label + " " + Advance().Lexeme;
            }
//            Consume(TokenType.Identifier, "Expect string after '['.").Lexeme;
            Consume(TokenType.RightBracket, "Expect ']' after label.");
            return label;
        }

        private void GetDiceModifiers(Expr faces, out Expr.Dice.DiceMod mod, out Expr.Dice.ModOperator modOp, out Expr modValue)
        {
            Token tMod = Previous();
            switch (tMod.Lexeme)
            {
                case "r":
                    mod = Expr.Dice.DiceMod.ReRoll;
                    break;
                case "!":
                case "x":
                    mod = Expr.Dice.DiceMod.Explode;
                    break;
                case "!!":
                case "cx":
                    mod = Expr.Dice.DiceMod.CompoundExplode;
                    break;
                case "cs":
                    mod = Expr.Dice.DiceMod.CountSuccesses;
                    break;
                case "ms":
                    mod = Expr.Dice.DiceMod.MarginOfSuccess;
                    break;
                case "kh":
                    mod = Expr.Dice.DiceMod.KeepHighest;
                    break;
                case "kl":
                    mod = Expr.Dice.DiceMod.KeepLowest;
                    break;
                case "dh":
                    mod = Expr.Dice.DiceMod.DropHighest;
                    break;
                case "dl":
                    mod = Expr.Dice.DiceMod.DropLowest;
                    break;
                default:
                    throw new ParserException($"Expected dice modifier, got '{tMod}'.");
            }

            // Now there should be a operator
            Token tModOp = Peek();
            modValue = null;
            switch (tModOp.Type)
            {
                case TokenType.Equal:
                    modOp = Expr.Dice.ModOperator.Equal;
                    Advance();
                    break;
                case TokenType.Less:
                    modOp = Expr.Dice.ModOperator.Less;
                    Advance();
                    break;
                case TokenType.LessEqual:
                    modOp = Expr.Dice.ModOperator.LessEqual;
                    Advance();
                    break;
                case TokenType.Greater:
                    modOp = Expr.Dice.ModOperator.Greater;
                    Advance();
                    break;
                case TokenType.GreaterEqual:
                    modOp = Expr.Dice.ModOperator.GreaterEqual;
                    Advance();
                    break;
                case TokenType.Number:
                    modOp = Expr.Dice.ModOperator.Equal;
                    modValue = Primary();
                    break;
                default:
                    if (mod == Expr.Dice.DiceMod.CountSuccesses)
                    {
                        modOp = Expr.Dice.ModOperator.Equal;
                        modValue = faces;
                    }
                    else
                    {
                        modOp = Expr.Dice.ModOperator.Equal;
                        modValue = new Expr.Literal(1.0d);
                    }
                    break;
            }

            if (modValue == null)
            {
                modValue = Call();
            }

            if (mod == Expr.Dice.DiceMod.KeepHighest || mod == Expr.Dice.DiceMod.KeepLowest ||
                mod == Expr.Dice.DiceMod.DropHighest || mod == Expr.Dice.DiceMod.DropLowest)
            {
                if (modOp != Expr.Dice.ModOperator.Equal)
                {
                    throw new ParserException($"{mod} only makes sense with operator equal.");
                }
            }
        }

        private Expr Unary()
        {
            if (Match(TokenType.Bang, TokenType.Minus))
            {
                Token @operator = Previous();
                Expr right = Unary();
                return new Expr.Unary(@operator, right);
            }

            return Call();
        }

        private Expr Call(bool consumeLabel = true)
        {
            Expr expr = Primary(consumeLabel);

            while (true)
            {
                Token function = Previous();
                if (Match(TokenType.LeftParen))
                {
                    expr = FinishCall(expr, function);
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private Expr FinishCall(Expr callee, Token function)
        {
            IList<Expr> arguments = new List<Expr>();
            if (!Check(TokenType.RightParen))
            {
                do
                {
                    arguments.Add(Expression());
                } while (Match(TokenType.Comma));
            }

            Token paren = Consume(TokenType.RightParen, "Expect ')' after arguments");
            return new Expr.Call(callee, function, paren, arguments);
        }

        private Expr Primary(bool consumeLabel = true)
        {
            if (Match(TokenType.False)) return new Expr.Literal(false);
            if (Match(TokenType.True)) return new Expr.Literal(true);
            
            if (Match(TokenType.Number, TokenType.Substitution))
            {
                return new Expr.Literal(Previous().Literal, Previous().Type == TokenType.Substitution, consumeLabel ? GetLabel() : null);
            }

            if (Match(TokenType.LeftBrace))
            {
                IList<Expr> arguments = new List<Expr>();
                if (!Check(TokenType.RightBrace))
                {
                    do
                    {
                        arguments.Add(Expression());
                    } while (Match(TokenType.Comma));
                }

                Consume(TokenType.RightBrace, "Expect '}' after dice pool parts.");
                GetDicePoolModifiers(out var mod, out var modOp, out var modValue);
                return new Expr.DicePool(arguments, mod, modOp, modValue);
            }

            if (Match(TokenType.LeftParen))
            {
                Expr expr = Expression();
                Consume(TokenType.RightParen, "Expect ')' after expression.");
                return new Expr.Grouping(expr);
            }

            return new Expr.Literal(Advance().Lexeme);
        }

        private void GetDicePoolModifiers(out Expr.DicePool.DicePoolMod mod, out Expr.DicePool.ModOperator modOp, out Expr modValue)
        {
            Token tMod = Consume(TokenType.Identifier, "Expect dice pool modifier after dice pool.");
            switch (tMod.Lexeme)
            {
                case "kh":
                    mod = Expr.DicePool.DicePoolMod.KeepHighest;
                    break;
                case "kl":
                    mod = Expr.DicePool.DicePoolMod.KeepLowest;
                    break;
                case "dh":
                    mod = Expr.DicePool.DicePoolMod.DropHighest;
                    break;
                case "dl":
                    mod = Expr.DicePool.DicePoolMod.DropLowest;
                    break;
                case "cs":
                    mod = Expr.DicePool.DicePoolMod.CountSuccesses;
                    break;
                default:
                    throw new ParserException($"Expected dice pool modifier, got '{tMod}'.");
            }


            // Now there should be a operator
            Token tModOp = Peek();
            modValue = null;
            switch (tModOp.Type)
            {
                case TokenType.Equal:
                    modOp = Expr.DicePool.ModOperator.Equal;
                    Advance();
                    break;
                case TokenType.Less:
                    modOp = Expr.DicePool.ModOperator.Less;
                    Advance();
                    break;
                case TokenType.LessEqual:
                    modOp = Expr.DicePool.ModOperator.LessEqual;
                    Advance();
                    break;
                case TokenType.Greater:
                    modOp = Expr.DicePool.ModOperator.Greater;
                    Advance();
                    break;
                case TokenType.GreaterEqual:
                    modOp = Expr.DicePool.ModOperator.GreaterEqual;
                    Advance();
                    break;
                case TokenType.Number:
                    modOp = Expr.DicePool.ModOperator.Equal;
                    modValue = Primary();
                    break;
                default:
                    modOp = Expr.DicePool.ModOperator.Equal;
                    modValue = new Expr.Literal(1.0d);
                    break;
                    //throw new ParserException($"Expected dice mod operator, got '{tModOp}'.");
            }

            if (modValue == null)
            {
                modValue = Call();
            }

            if (mod == Expr.DicePool.DicePoolMod.KeepHighest || mod == Expr.DicePool.DicePoolMod.KeepLowest ||
                mod == Expr.DicePool.DicePoolMod.DropHighest || mod == Expr.DicePool.DicePoolMod.DropLowest)
            {
                if (modOp != Expr.DicePool.ModOperator.Equal)
                {
                    throw new ParserException($"{mod} only makes sense with operator equal.");
                }
            }
        }
    }

    public abstract class Expr
    {
        public interface IVisitor<TR>
        {
            TR VisitTertiaryExpr(Tertiary expr);
            TR VisitBinaryExpr(Binary expr);
            TR VisitUnaryExpr(Unary expr);
            TR VisitGroupingExpr(Grouping expr);
            TR VisitLiteralExpr(Literal expr);
            TR VisitDiceExpr(Dice dice);
            TR VisitDicePoolExpr(DicePool pool);
            TR VisitCallExpr(Call expr);
        }

        public abstract TR Accept<TR>(IVisitor<TR> visitor);

        public class Dice : Expr
        {
            public enum DiceMod
            {
                None,
                KeepHighest,
                KeepLowest,
                DropHighest,
                DropLowest,
                CountSuccesses,
                MarginOfSuccess,
                Explode,
                CompoundExplode,
                ReRoll,
            }

            public enum ModOperator
            {
                Equal,
                Less,
                LessEqual,
                Greater,
                GreaterEqual
            }
            public Expr Num { get; }
            public Expr Faces { get; }
            public DiceMod Mod { get; }
            public ModOperator ModOp { get; }
            public Expr ModValue { get; }
            public string Label { get; }

            public List<double> Results { get; }
            public double Result { get; set; }

            public Dice(Expr num, Expr faces, string label)
            {
                Num = num;
                Faces = faces;
                Mod = DiceMod.None;
                ModOp = ModOperator.Equal;
                ModValue = null;
                Results = new List<double>();
                Result = -1;
                Label = label;
            }

            public Dice(Expr num, Expr faces, string label, DiceMod mod, ModOperator modOp, Expr modValue)
            {
                Num = num;
                Faces = faces;
                Mod = mod;
                ModOp = modOp;
                ModValue = modValue;
                Results = new List<double>();
                Result = -1;
                Label = label;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitDiceExpr(this);
            }
        }

        public class DicePool : Expr
        {
            public enum DicePoolMod
            {
                KeepHighest,
                KeepLowest,
                DropHighest,
                DropLowest,
                CountSuccesses
            }

            public enum ModOperator
            {
                Equal,
                Less,
                LessEqual,
                Greater,
                GreaterEqual
            }
            public IList<Expr> Arguments { get; }
            public DicePoolMod Mod { get; }
            public ModOperator ModOp { get; }
            public Expr ModValue { get; }

            public DicePool(IList<Expr> arguments, DicePoolMod mod, ModOperator modOp, Expr modValue)
            {
                Arguments = arguments;
                Mod = mod;
                ModOp = modOp;
                ModValue = modValue;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitDicePoolExpr(this);
            }
        }

        public class Tertiary : Expr
        {
            public Expr Condition { get; }
            public Expr Then { get; }
            public Expr Else { get; }

            public Tertiary(Expr condition, Expr then, Expr @else)
            {
                Condition = condition;
                Then = then;
                Else = @else;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitTertiaryExpr(this);
            }
        }

        public class Binary : Expr
        {
            public Expr Left { get; }
            public Expr Right { get; }
            public Token Operator { get; }

            public Binary(Expr left, Token @operator, Expr right)
            {
                Left = left;
                Right = right;
                Operator = @operator;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitBinaryExpr(this);
            }
        }

        public class Call : Expr
        {
            public Expr Callee { get; }
            public Token Function { get; }
            public Token Paren { get; }
            public IList<Expr> Arguments { get; }

            public Call(Expr callee, Token function, Token paren, IList<Expr> arguments)
            {
                Callee = callee;
                Function = function;
                Paren = paren;
                Arguments = arguments;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitCallExpr(this);
            }
        }

        public class Unary : Expr
        {
            public Expr Right { get; }
            public Token Operator { get; }
            public Unary(Token @operator, Expr right)
            {
                Right = right;
                Operator = @operator;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitUnaryExpr(this);
            }
        }

        public class Grouping : Expr
        {
            public Expr Expression { get; }
            public Grouping(Expr expression)
            {
                Expression = expression;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitGroupingExpr(this);
            }
        }

        public class Literal : Expr
        {
            public object Value { get; set; }
            public bool IsSubstitution { get; set; }
            public string Label { get; }
            public Literal(object value, bool isSubstitution = false, string label = null)
            {
                Value = value;
                IsSubstitution = isSubstitution;
                Label = label;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitLiteralExpr(this);
            }
        }
    }

    public class AstPrinter : Expr.IVisitor<string>
    {
        private IDictionary<string, object> _context;

        public string Print(Expr expr, IDictionary<string, object> context)
        {
            _context = context;
            return expr.Accept(this);
        }

        public string VisitTertiaryExpr(Expr.Tertiary expr)
        {
            return Parenthesize("?:", expr.Condition, expr.Then, expr.Else);
        }

        public string VisitBinaryExpr(Expr.Binary expr)
        {
            return Parenthesize(expr.Operator.Lexeme, expr.Left, expr.Right);
        }

        public string VisitUnaryExpr(Expr.Unary expr)
        {
            return Parenthesize(expr.Operator.Lexeme, expr.Right);
        }

        public string VisitGroupingExpr(Expr.Grouping expr)
        {
            return Parenthesize("group", expr.Expression);
        }

        public string VisitLiteralExpr(Expr.Literal expr)
        {
            if (expr.Value is double d)
            {
                return string.IsNullOrEmpty(expr.Label) ? d.ToString(NumberFormatInfo.InvariantInfo) : $"{d.ToString(NumberFormatInfo.InvariantInfo)}[{expr.Label}]";
            }

            if (expr.IsSubstitution)
            {
                if (!(expr.Value is string))
                {
                    throw new Parser.ParserException("Substitution must be a string.");
                }

                if (_context == null || !_context.ContainsKey(expr.Value.ToString()))
                {
                    return $"(@ {expr.Value} !KEYNOTFOUND!)";
                }

                return $"(@ {expr.Value} {_context[expr.Value.ToString()]})";
            }
            return string.IsNullOrEmpty(expr.Label) ? expr.Value.ToString() : $"{expr.Value}[{expr.Label}]";
        }

        public string VisitDiceExpr(Expr.Dice expr)
        {
            var label = string.IsNullOrEmpty(expr.Label) ? "" : $" [{expr.Label}]";

            if (expr.Mod == Expr.Dice.DiceMod.None)
                return $"(d {expr.Num.Accept(this)} {expr.Faces.Accept(this)}{label})";

            return $"(d {expr.Num.Accept(this)} {expr.Faces.Accept(this)} {expr.Mod} {expr.ModOp} {expr.ModValue.Accept(this)}{label})";
        }

        public string VisitDicePoolExpr(Expr.DicePool expr)
        {
            return $"(dP {{{expr.Arguments.Aggregate("", (s, expr1) => string.IsNullOrEmpty(s) ? $"{expr1.Accept(this)}" : s + $", {expr1.Accept(this)}")}}} {expr.Mod} {expr.ModOp} {expr.ModValue.Accept(this)})";
        }

        public string VisitCallExpr(Expr.Call expr)
        {
            return Parenthesize((expr.Callee as Expr.Literal)?.Value.ToString(), expr.Arguments.ToArray());
        }

        private string Parenthesize(string name, params Expr[] exprs)
        {
            var s = $"({name}";
            s = exprs.Aggregate(s, (current, expr) => current + $" {expr.Accept(this)}");
            s += ")";

            return s;
        }
    }
}
