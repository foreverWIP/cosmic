using Pidgin;
using Pidgin.Comment;
using static Pidgin.Parser<char>;
using static Pidgin.Parser;

namespace Cosmic.ScriptConverter;



static class Parsers
{
    static readonly Parser<char, CommentNode> comment =
        Char('/').Repeat(2)
        .Then(
            Any.ManyString().Until(EndOfLine),
            (ie, u) => new CommentNode(new string(ie.ToArray()))
        );

    static readonly Parser<char, AliasNode> alias =
        String("#alias")
        .Then(
            Any.ManyString().Between(SkipWhitespaces),
            (k, v) => v
        )
        .Then(
            Char(':').Between(SkipWhitespaces),
            (v, c) => v
        )
        .Then(
            Any.ManyString().Between(SkipWhitespaces),
            (v, n) => new AliasNode(n, v)
        );


}