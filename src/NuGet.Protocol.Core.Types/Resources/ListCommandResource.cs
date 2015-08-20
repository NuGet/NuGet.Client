using System;

namespace NuGet.Protocol.Core.Types
{
    public class ListCommandResource : INuGetResource
    {
        private readonly string _listEndpoint;

        public ListCommandResource(string listEndpoint)
        {
            if (listEndpoint == null)
            {
                throw new ArgumentNullException(nameof(listEndpoint));
            }

            _listEndpoint = listEndpoint;
        }

        public string GetListEndpoint()
        {
            return _listEndpoint;
        }
    }
}
