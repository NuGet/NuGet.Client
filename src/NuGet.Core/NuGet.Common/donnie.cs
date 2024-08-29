#nullable enable

#pragma warning disable RS0036 // Annotate nullability of public types and members in the declared API
namespace Microsoft.VisualStudio.ProjectSystem
{
    public class UnconfiguredProject
    {
        public UnconfiguredProject(IVsBrowseObjectContext arg)
        { }
    }

    public class ProjectCapabilities
    { }

    public class ProjectService
    { }

    public class IVsBrowseObjectContext
    {
        public IVsBrowseObjectContext? VsBrowseObjectContext;
    }

    public class SharedAssetsProject
    { }
}
#pragma warning restore RS0036 // Annotate nullability of public types and members in the declared API
