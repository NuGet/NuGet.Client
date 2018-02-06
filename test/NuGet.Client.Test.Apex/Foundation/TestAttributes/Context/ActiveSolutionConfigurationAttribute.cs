namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public class ActiveSolutionConfigurationAttribute : ContextTraitAttribute
    {
        public ActiveSolutionConfigurationAttribute(ActiveSolutionConfiguration solutionConfiguration)
            : base("SolutionConfiguration", solutionConfiguration.ToString())
        {
            this.SolutionConfiguration = solutionConfiguration;
        }

        public ActiveSolutionConfiguration SolutionConfiguration { get; private set; }

        public override Context CreateContext(Context defaultContext)
        {
            return new Context(
                defaultContext,
                solutionConfiguration: this.SolutionConfiguration);
        }

        public override string GenerateStringValue()
        {
            return this.SolutionConfiguration.ToString();
        }
    }
}
