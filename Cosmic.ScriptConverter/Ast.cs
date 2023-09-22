namespace Cosmic.ScriptConverter;

record AstNode(
    uint Line = 0,
    uint Column = 0
);

record CommentNode(
    string Text
) : AstNode;

record AliasNode(
    string Name,
    string Value
) : AstNode;