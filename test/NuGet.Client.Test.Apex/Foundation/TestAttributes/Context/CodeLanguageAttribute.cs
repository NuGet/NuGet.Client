namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public class CodeLanguageAttribute : ContextTraitAttribute
    {
        public CodeLanguageAttribute(CodeLanguage language)
            : base("Language", language.ToString())
        {
            this.Language = language;
        }

        public CodeLanguage Language { get; private set; }

        public override Context CreateContext(Context defaultContext)
        {
            return new Context(
                defaultContext,
                language: this.Language);
        }

        public override string GenerateStringValue()
        {
            return Language.ToString();
        }

        public override bool Equals(object obj)
        {
            CodeLanguageAttribute rhs = obj as CodeLanguageAttribute;
            if (rhs == null)
            {
                return false;
            }

            return this.Language == rhs.Language;
        }

        public override int GetHashCode()
        {
            return (this.Language.ToString()).GetHashCode();
        }
    }
}
