namespace NuGetClient.Test.Foundation.TestAttributes.Context
{
    public class PlatformAttribute : ContextTraitAttribute
    {
        public PlatformAttribute(PlatformIdentifier platform, PlatformVersion version = PlatformVersion.LatestVersion)
            : base("Platform", string.Join(" ", platform.ToString(), version.ToString()))
        {
            this.Platform = platform;
            this.Version = version;
        }

        public PlatformIdentifier Platform { get; private set; }
        public PlatformVersion Version { get; private set; }
        public override Context CreateContext(Context defaultContext)
        {
            return new Context(
                defaultContext,
                this.Platform,
                this.Version);
        }

        public override string GenerateStringValue()
        {
            return string.Join(" ", this.Platform.ToString(), this.Version.ToString());
        }

        public override bool Equals(object obj)
        {
            PlatformAttribute rhs = obj as PlatformAttribute;
            if (rhs == null)
            {
                return false;
            }

            return this.Platform == rhs.Platform && this.Version == rhs.Version;
        }

        public override int GetHashCode()
        {
            return string.Join(" ", this.Platform.ToString(), this.Version.ToString()).GetHashCode();
        }
    }
}
