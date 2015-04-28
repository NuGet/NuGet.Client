using NuGet.LibraryModel;

namespace NuGet.DependencyResolver
{
    public class RemoteMatch : IEquatable<RemoteMatch>
    {
        public IRemoteDependencyProvider Provider { get; set; }
        public LibraryIdentity Library { get; set; }
        public string Path { get; set; }

        public override bool Equals(object obj)
        {
            return Equals(obj as RemoteMatch);
        }

        public bool Equals(RemoteMatch other)
        {
            return other != null && Library.Equals(other.Library);
        }

        public override int GetHashCode()
        {
            return Library.GetHashCode();
        }
    }
}
