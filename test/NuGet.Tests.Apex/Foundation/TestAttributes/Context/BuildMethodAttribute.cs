namespace NuGet.Tests.Foundation.TestAttributes.Context
{
    public class BuildMethodAttribute : ContextTraitAttribute
    {
        public BuildMethodAttribute(BuildMethod buildMethod)
            : base("BuildMethod", buildMethod.ToString())
        {
            this.BuildMethod = buildMethod;
        }

        public BuildMethod BuildMethod { get; private set; }

        public override Context CreateContext(Context defaultContext)
        {
            return new Context(
                defaultContext,
                buildMethod: this.BuildMethod);
        }

        public override string GenerateStringValue()
        {
            return this.BuildMethod.ToString();
        }
    }
}
