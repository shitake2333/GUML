namespace GUML.Shared.Syntax;

/// <summary>
/// Enumerates all token kinds, trivia kinds, and syntax node kinds in the GUML language.
/// </summary>
public enum SyntaxKind : ushort
{
    // === Special ===
    None = 0,
    EndOfFileToken,
    BadToken,

    // === Trivia ===
    WhitespaceTrivia,
    EndOfLineTrivia,
    SingleLineCommentTrivia,
    DocumentationCommentTrivia,
    SkippedTokensTrivia,

    // === Punctuators ===
    OpenBraceToken, // {
    CloseBraceToken, // }
    OpenParenToken, // (
    CloseParenToken, // )
    OpenBracketToken, // [
    CloseBracketToken, // ]
    CommaToken, // ,
    DotToken, // .
    ColonToken, // :
    PipeToken, // |
    QuestionToken, // ?
    FatArrowToken, // =>
    MapToPropertyToken, // :=
    MapToDataToken, // =:
    MapTwoWayToken, // <=>

    // === Operators ===
    PlusToken, // +
    MinusToken, // -
    AsteriskToken, // *
    SlashToken, // /
    PercentToken, // %
    BangToken, // !
    EqualsEqualsToken, // ==
    BangEqualsToken, // !=
    LessThanToken, // <
    GreaterThanToken, // >
    LessThanEqualsToken, // <=
    GreaterThanEqualsToken, // >=
    BarBarToken, // ||
    AmpersandAmpersandToken, // &&

    // === Keywords ===
    ImportKeyword,
    AsKeyword,
    ParamKeyword,
    EventKeyword,
    EachKeyword,
    NewKeyword,
    ImageKeyword,
    FontKeyword,
    AudioKeyword,
    VideoKeyword,

    // === Literals ===
    StringLiteralToken,
    TemplateStringLiteralToken,
    IntegerLiteralToken,
    FloatLiteralToken,
    TrueLiteralToken,
    FalseLiteralToken,
    NullLiteralToken,

    // === Identifiers ===
    IdentifierToken,
    ComponentNameToken,
    GlobalRefToken, // $xxx
    AliasRefToken, // @xxx
    EventRefToken, // #xxx
    EnumValueToken, // .PascalCase

    // === Syntax Nodes: Top-level ===
    GumlDocument,
    ImportDirective,
    ImportAlias,

    // === Syntax Nodes: Components ===
    ComponentDeclaration,
    AliasPrefix,
    DocumentationComment,

    // === Syntax Nodes: Members ===
    PropertyAssignment,
    MappingAssignment,
    EventSubscription,
    TemplateParamAssignment,
    ParameterDeclaration,
    EventDeclaration,

    // === Syntax Nodes: Each ===
    EachBlock,
    EachParams,

    // === Syntax Nodes: Expressions ===
    LiteralExpression,
    ReferenceExpression,
    MemberAccessExpression,
    BinaryExpression,
    PrefixUnaryExpression,
    ConditionalExpression,
    CallExpression,
    StructExpression,
    ResourceExpression,
    ObjectCreationExpression,
    ArrayLiteralExpression,
    DictionaryLiteralExpression,
    TypedDictionaryLiteralExpression,
    TemplateStringExpression,
    TemplateStringInterpolation,
    TemplateStringText,
    ParenthesizedExpression,
    EnumValueExpression,
    ObjectLiteralExpression,

    // === Syntax Nodes: Auxiliary ===
    ArgumentList,
    EventArgumentList,
    EventArgument,
    DictionaryEntry,
    SkippedTokens,
}
