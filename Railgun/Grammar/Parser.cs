﻿using System.Collections.Generic;
using System.Linq;
using Railgun.Api;
using Railgun.Types;

namespace Railgun.Grammar
{
    public abstract class BaseParser
    {
        protected int Pos;
        protected List<Token> Tokens;
        protected Token Current => Tokens[Pos];
        
        protected Token Next()
        {
            var current = Current;
            Pos++;
            return current;
        }

        protected void MustBe(TokenType t)
        {
            if (Current.Kind != t)
            {
                throw new ParseException($"Expected {t.ToString()}, Got {Current.Kind}", Current.Position);
            }

            Pos++;
        }
    }
    
    public class Parser : BaseParser
    {
        protected string Source;
        public Parser(string source)
        {
            Source = source;
            var lexer = new Lexer(source);
            Tokens = lexer.Lex();
        }
        
        public Parser(List<Token> tokens)
        {
            Tokens = tokens;
        }

        // a program is just a list of exprs
        public object[] ParseProgram()
        {
            List<object> objs = new();
            while (Current.Kind != TokenType.Eof)
            {
                objs.Add(ParseExpr());
            }
            return objs.ToArray();
        }

        public object ParseExpr()
        {
            switch (Current.Kind)
            {
                case TokenType.LParen:
                    return ParseSequence();
                case TokenType.LBracket:
                    return ParseList();
                case TokenType.LBrace:
                    return ParseDict();
                case TokenType.RParen:
                case TokenType.RBracket:
                    throw new ParseException($"Unexpected \"{Current.Value}\"", Current.Position);
                case TokenType.Quote:
                    Pos++;
                    return Seq.Create(new[] { new NameExpr("quote"), ParseExpr() });
                case TokenType.UnquoteSplice:
                    Pos++;
                    return Seq.Create(new[] { new NameExpr("splice"), ParseExpr() });
                case TokenType.Quasiquote:
                    Pos++;
                    return Seq.Create(new[] { new NameExpr("quasiquote"), ParseExpr() });
                case TokenType.Unquote:
                    Pos++;
                    return Seq.Create(new[] { new NameExpr("unquote"), ParseExpr() });
                case TokenType.Keyword:
                    return new Keyword(Next().Value);
                case TokenType.NameSymbol:
                    var nv = Next().Value;
                    if (nv.Contains("."))
                    {
                        var s = nv.Split('.');
                        var o = new List<object> { new NameExpr("."), new NameExpr(s.First()) };
                        o.AddRange(s.Skip(1));
                        return Seq.Create(o);
                    }
                    return new NameExpr(nv);
                case TokenType.Numeric:
                    var numeric = Next().Value;
                    if (numeric.Contains('.'))
                    {
                        return double.Parse(numeric);
                    }
                    return int.Parse(numeric);
                case TokenType.String:
                    return Next().Value;
                default:
                    throw new ParseException("Unknown Token Type " + Current.Kind, Current.Position);
            }
        }

        private void TakeIndents()
        {
            while (true)
            {
                switch (Current.Kind)
                {
                    case TokenType.Indent:
                    case TokenType.Dedent:
                    case TokenType.Newline:
                        Pos++;
                        break;
                    default:
                        return;
                }
            }
        }

        private List<object> ParseCollection(TokenType left, TokenType right)
        {
            var list = new List<object>();
            MustBe(left);
            TakeIndents();
            while (Current.Kind != right)
            {
                list.Add(ParseExpr());
                TakeIndents();
            }
            MustBe(right);
            return list;
        }

        public Seq ParseList()
        {
            var w = new[]
            {
                new NameExpr("list"),
            }.Concat(ParseCollection(TokenType.LBracket, TokenType.RBracket));
            return Seq.Create(w);
        }
        
        public Seq ParseDict()
        {
            var w = new[]
            {
                new NameExpr("dict"),
            }.Concat(ParseCollection(TokenType.LBrace, TokenType.RBrace));
            return Seq.Create(w);
        }

        public Seq ParseSequence()
        {
            return Seq.Create(ParseCollection(TokenType.LParen, TokenType.RParen));
        }
    }
}