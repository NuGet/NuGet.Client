using NuGet.Resolver;
namespace NuGet.PackageManagement.UI
{
    // Represents an item in the DependencyBehavior combobox.
    public class DependencyBehaviorItem
    {
        public string Text
        {
            get;
            private set;
        }

        public DependencyBehavior Behavior
        {
            get;
            private set;
        }

        public DependencyBehaviorItem(string text, DependencyBehavior dependencyBehavior)
        {
            Text = text;
            Behavior = dependencyBehavior;
        }

        public override string ToString()
        {
            return Text;
        }
    }
}
