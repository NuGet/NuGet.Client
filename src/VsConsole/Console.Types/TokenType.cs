namespace NuGetConsole
{
    /// <summary>
    /// General token types expected by PowerConsole.
    /// </summary>
    public enum TokenType
    {
        /// <summary>
        /// A character literal.
        /// </summary>
        CharacterLiteral,

        /// <summary>
        /// A comment.
        /// </summary>
        Comment,

        /// <summary>
        /// Excluded code, such as excluded by pragma.
        /// </summary>
        ExcludedCode,

        /// <summary>
        /// Formal language.
        /// </summary>
        FormalLanguage,

        /// <summary>
        /// An identifier.
        /// </summary>
        Identifier,

        /// <summary>
        /// A keyword.
        /// </summary>
        Keyword,

        /// <summary>
        /// A general literal.
        /// </summary>
        Literal,

        /// <summary>
        /// Natual language.
        /// </summary>
        NaturalLanguage,

        /// <summary>
        /// A number literal.
        /// </summary>
        NumberLiteral,

        /// <summary>
        /// An operator.
        /// </summary>
        Operator,

        /// <summary>
        /// Other token type.
        /// </summary>
        Other,

        /// <summary>
        /// A preprocessor keyword.
        /// </summary>
        PreprocessorKeyword,

        /// <summary>
        /// A string literal.
        /// </summary>
        StringLiteral,

        /// <summary>
        /// A symbol definition.
        /// </summary>
        SymbolDefinition,

        /// <summary>
        /// A symbol reference.
        /// </summary>
        SymbolReference,

        /// <summary>
        /// White space.
        /// </summary>
        WhiteSpace,
    }
}
