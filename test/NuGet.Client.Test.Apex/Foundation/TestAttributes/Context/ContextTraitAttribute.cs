namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public abstract class ContextTraitAttribute : TraitAttribute
    {
        public ContextTraitAttribute(string name, string value) : base(name,value)
        { }

        public abstract Context CreateContext(Context defaultContext);
        public abstract string GenerateStringValue();

        public override string ToString()
        {
            return this.GenerateStringValue();
        }
    }
}
