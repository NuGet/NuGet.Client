using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NuGet.Client;
using Xunit;


namespace Client.V3Test
{
    public class ClientV3Test
    {
        private NuGet.Client.V3.NuGetV3Client V3Client;
        private string PreviewRootUrl = "https://az320820.vo.msecnd.net/ver3-preview/index.json";
        public ClientV3Test()
        {
            V3Client = new NuGet.Client.V3.NuGetV3Client(PreviewRootUrl,"TestApp");
            
        }

        [Fact]
        public async Task TestSearchAutoComplete()
        {
           IEnumerable<string> searchTokens = await V3Client.SearchAutocomplete("elm", new System.Threading.CancellationToken());
           Assert.True(searchTokens.Count() > 0);
            //check if all items contains the given search text.
           Assert.False(searchTokens.Any(item => item.IndexOf("elm", StringComparison.OrdinalIgnoreCase) == -1));
        }
    }
}
