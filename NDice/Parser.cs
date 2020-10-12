using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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

            while (Match(TokenType.Slash, TokenType.Star))
            {
                Token @operator = Previous();
                Expr right = Unary();
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
                    Expr eyes = Call();

                    // Check for modifiers
                    if (Match(TokenType.Identifier))
                    {
                        Token tMod = Previous();
                        Expr.Dice.DiceMod mod;
                        switch (tMod.Lexeme)
                        {
                            case "r":
                                mod = Expr.Dice.DiceMod.ReRoll;
                                break;
                            case "x":
                                mod = Expr.Dice.DiceMod.Explode;
                                break;
                            case "cs":
                                mod = Expr.Dice.DiceMod.CountSuccesses;
                                break;
                            case "ms":
                                mod = Expr.Dice.DiceMod.MarginOfSuccess;
                                break;
                            default:
                                throw new ParserException($"Expected dice modifier, got '{tMod}'.");
                        }

                        // Now there should be a operator
                        Token tModOp = Advance();
                        Expr.Dice.ModOperator modOp;
                        switch (tModOp.Type)
                        {
                            case TokenType.Equal:
                                modOp = Expr.Dice.ModOperator.Equal;
                                break;
                            case TokenType.Less:
                                modOp = Expr.Dice.ModOperator.Less;
                                break;
                            case TokenType.LessEqual:
                                modOp = Expr.Dice.ModOperator.LessEqual;
                                break;
                            case TokenType.Greater:
                                modOp = Expr.Dice.ModOperator.Greater;
                                break;
                            case TokenType.GreaterEqual:
                                modOp = Expr.Dice.ModOperator.GreaterEqual;
                                break;
                            default:
                                throw new ParserException($"Expected dice mod operator, got '{tModOp}'.");
                        }

                        Expr modValue = Call();
                        expr = new Expr.Dice(expr, eyes, mod, modOp, modValue);
                    }
                    else
                    {
                        expr = new Expr.Dice(expr, eyes);
                    }
                }
            }

            return expr;
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

        private Expr Call()
        {
            Expr expr = Primary();

            while (true)
            {
                if (Match(TokenType.LeftParen))
                {
                    expr = FinishCall(expr);
                }
                else
                {
                    break;
                }
            }

            return expr;
        }

        private Expr FinishCall(Expr callee)
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
            return new Expr.Call(callee, paren, arguments);
        }

        private Expr Primary()
        {
            if (Match(TokenType.False)) return new Expr.Literal(false);
            if (Match(TokenType.True)) return new Expr.Literal(true);
            
            if (Match(TokenType.Number, TokenType.Substitution))
            {
                return new Expr.Literal(Previous().Literal, Previous().Type == TokenType.Substitution);
            }

            if (Match(TokenType.LeftParen))
            {
                Expr expr = Expression();
                Consume(TokenType.RightParen, "Expect ')' after expression.");
                return new Expr.Grouping(expr);
            }

            return new Expr.Literal(Advance().Lexeme);
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
            TR VisitCallExpr(Call expr);
        }

        public abstract TR Accept<TR>(IVisitor<TR> visitor);

        public class Dice : Expr
        {
            public enum DiceMod
            {
                None,
                CountSuccesses,
                MarginOfSuccess,
                Explode,
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

            public Dice(Expr num, Expr faces)
            {
                Num = num;
                Faces = faces;
                Mod = DiceMod.None;
                ModOp = ModOperator.Equal;
                ModValue = null;
            }

            public Dice(Expr num, Expr faces, DiceMod mod, ModOperator modOp, Expr modValue)
            {
                Num = num;
                Faces = faces;
                Mod = mod;
                ModOp = modOp;
                ModValue = modValue;
            }

            public override TR Accept<TR>(IVisitor<TR> visitor)
            {
                return visitor.VisitDiceExpr(this);
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
            public Token Paren { get; }
            public IList<Expr> Arguments { get; }

            public Call(Expr callee, Token paren, IList<Expr> arguments)
            {
                Callee = callee;
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
            public object Value { get; }
            public bool IsSubstitution { get; }
            public Literal(object value, bool isSubstitution = false)
            {
                Value = value;
                IsSubstitution = isSubstitution;
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
                return d.ToString(NumberFormatInfo.InvariantInfo);
            }

            if (expr.IsSubstitution)
            {
                if (!(expr.Value is string))
                {
                    throw new Parser.ParserException("Substitution must be a string.");
                }

                return $"(@ {expr.Value} {_context[expr.Value.ToString()]})";
            }
            return expr.Value.ToString();
        }

        public string VisitDiceExpr(Expr.Dice expr)
        {
            if (expr.Mod == Expr.Dice.DiceMod.None)
                return Parenthesize("d", expr.Num, expr.Faces);

            return $"(d {expr.Num.Accept(this)} {expr.Faces.Accept(this)} {expr.Mod} {expr.ModOp} {expr.ModValue.Accept(this)})";
        }

        public string VisitCallExpr(Expr.Call expr)
        {
            return Parenthesize((expr.Callee as Expr.Literal)?.Value.ToString(), expr.Arguments.ToArray());
        }

        private string Parenthesize(string name, params Expr[] exprs)
        {
            var s = $"({name}";
            foreach (var expr in exprs)
            {
                s += $" {expr.Accept(this)}";
            }

            s += ")";

            return s;
        }
    }
}
