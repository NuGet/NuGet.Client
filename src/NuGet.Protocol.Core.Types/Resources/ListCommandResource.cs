using System;

namespace NuGet.Protocol.Core.Types
{
    public class ListCommandResource : INuGetResource
    {
        private readonly string _listEndpoint;

        public ListCommandResource(string listEndpoint)
        {
            // _listEndpoint may be null
            _listEndpoint = listEndpoint;
        }

        public string GetListEndpoint()
        {
            return _listEndpoint;
        }
    }
}
