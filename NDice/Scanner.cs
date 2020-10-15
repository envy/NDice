using System;
using System.Collections.Generic;
using System.Globalization;

namespace NDice
{
    public enum TokenType
    {
        LeftParen,
        RightParen,
        LeftBrace,
        RightBrace,
        LeftBracket,
        RightBracket,
        Comma,
        Dot,
        Minus,
        Plus,
        Star,
        Slash,
        Percent,

        Number,
        Substitution,
        Identifier,

        Bang,
        BangBang,
        Less,
        Greater,
        Equal,
        BangEqual,
        EqualEqual,
        LessEqual,
        GreaterEqual,
        True,
        False,
        IfThen,
        Else,

        Eof
    }

    public class Token
    {
        public TokenType Type { get; }
        public string Lexeme { get; }
        public object Literal { get; }

        public Token(TokenType type, string lexeme, object literal)
        {
            Type = type;
            Lexeme = lexeme;
            Literal = literal;
        }

        public override string ToString()
        {
            return $"{Type} '{Lexeme}' {Literal:#.###}";
        }
    }

    public class ScannerException : Exception
    {
        public ScannerException(string message) : base(message)
        {
        }
    }

    public class Scanner
    {
        private readonly string _source;
        private readonly IList<Token> _tokens = new List<Token>();
        private static readonly IDictionary<string, TokenType> Keywords;
        private int _start;
        private int _current;

        static Scanner()
        {
            Keywords = new Dictionary<string, TokenType>
            {
                {"true", TokenType.True},
                {"false", TokenType.False},
            };
        }

        public Scanner(string source)
        {
            _source = source;
            _start = 0;
            _current = 0;
        }

        public IList<Token> ScanTokens()
        {
            while (!IsAtEnd())
            {
                _start = _current;
                ScanToken();
            }

            _tokens.Add(new Token(TokenType.Eof, "", null));
            return _tokens;
        }

        private bool IsAtEnd()
        {
            return _current >= _source.Length;
        }

        private void ScanToken()
        {
            var c = Advance();
            switch (c)
            {
                case '(':
                    AddToken(TokenType.LeftParen);
                    break;
                case ')':
                    AddToken(TokenType.RightParen);
                    break;
                case '{':
                    AddToken(TokenType.LeftBrace);
                    break;
                case '}':
                    AddToken(TokenType.RightBrace);
                    break;
                case '[':
                    AddToken(TokenType.LeftBracket);
                    break;
                case ']':
                    AddToken(TokenType.RightBracket);
                    break;
                case '+':
                    AddToken(TokenType.Plus);
                    break;
                case '-':
                    AddToken(TokenType.Minus);
                    break;
                case ',':
                    AddToken(TokenType.Comma);
                    break;
                case '.':
                    AddToken(TokenType.Dot);
                    break;
                case '*':
                    AddToken(TokenType.Star);
                    break;
                case '/':
                    AddToken(TokenType.Slash);
                    break;
                case '%':
                    AddToken(TokenType.Percent);
                    break;
                case '!':
                    if (Peek() == '!')
                    {
                        Advance();
                        AddToken(TokenType.BangBang);
                    }
                    else
                    {
                        AddToken(Match('=') ? TokenType.BangEqual : TokenType.Bang);
                    }
                    break;
                case '=':
                    AddToken(Match('=') ? TokenType.EqualEqual : TokenType.Equal);
                    break;
                case '<':
                    AddToken(Match('=') ? TokenType.LessEqual : TokenType.Less);
                    break;
                case '>':
                    AddToken(Match('=') ? TokenType.GreaterEqual : TokenType.Greater);
                    break;
                case '?':
                    AddToken(TokenType.IfThen);
                    break;
                case ':':
                    AddToken(TokenType.Else);
                    break;
                case ' ':
                case '\r':
                case '\t':
                    break;
                case '@':
                    Substitution();
                    break;
                default:
                    if (IsDigit(c))
                    {
                        Number();
                    }
                    else if (IsAlpha(c))
                    {
                        Identifier();
                    }
                    else
                    {
                        throw new ScannerException($"Unexpected character '{c}'.");
                    }
                    break;
            }
        }

        private void Identifier()
        {
            while (IsAlpha(Peek()))
                Advance();

            var text = _source.Substring(_start, _current - _start);
            AddToken(Keywords.TryGetValue(text, out var type) ? @type : TokenType.Identifier);
        }

        private bool IsAlpha(char c)
        {
            return (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || c == '_';
        }

        private bool IsAlphaNumeric(char c)
        {
            return IsAlpha(c) || IsDigit(c);
        }

        private bool IsDigit(char c)
        {
            return c >= '0' && c <= '9';
        }

        private void Number()
        {
            while (IsDigit(Peek()))
                Advance();

            if (Peek() == '.' && IsDigit(PeekNext()))
            {
                Advance();

                while (IsDigit(Peek()))
                {
                    Advance();
                }
            }

            AddToken(TokenType.Number, double.Parse(_source.Substring(_start, _current - _start), NumberStyles.Any, NumberFormatInfo.InvariantInfo));
        }

        private void Substitution()
        {
            while ((IsAlphaNumeric(Peek()) || Peek() == '.') && !IsAtEnd()) 
            {
                Advance();
            }

            var value = _source.Substring(_start + 1, _current - _start - 1);
            AddToken(TokenType.Substitution, value);
        }

        private char Peek()
        {
            if (IsAtEnd()) return '\0';
            return _source[_current];
        }

        private char PeekNext()
        {
            if (_current + 1 >= _source.Length)
                return '\0';
            return _source[_current + 1];
        }

        private bool Match(char expected)
        {
            if (IsAtEnd()) return false;
            if (_source[_current] != expected) return false;

            _current++;
            return true;
        }

        private char Advance()
        {
            _current++;
            return _source[_current - 1];
        }

        private void AddToken(TokenType type, object literal = null)
        {
            string text = _source.Substring(_start, _current - _start);
            _tokens.Add(new Token(type, text, literal));
        }
    }
}
