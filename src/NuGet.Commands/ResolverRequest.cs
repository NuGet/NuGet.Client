using NuGet.LibraryModel;

namespace NuGet.Commands
{
    public class ResolverRequest
    {
        public LibraryIdentity Requestor { get; }
        public LibraryRange Request { get; }

        public ResolverRequest(LibraryIdentity requestor, LibraryRange request)
        {
            Requestor = requestor;
            Request = request;
        }

        public override string ToString()
        {
            return Strings.FormatResolverRequest_ToStringFormat(Request.ToString(), Requestor.ToString());
        }
    }
}