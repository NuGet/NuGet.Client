using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Protocol.Core.Types;
using Test.Utility;
using Xunit;


namespace NuGet.Protocol.VisualStudio.Tests
{
    // Tests the Powershell autocomplete resource for V2 and v3 sources.  
    public class PowershellAutoCompleteResourceTests
    {
        private static Dictionary<string, string> ResponsesDict;
        public PowershellAutoCompleteResourceTests()
        {
            ResponsesDict = new Dictionary<string, string>();
            ResponsesDict.Add("http://testsource.com/v3/index.json", JsonData.IndexJson);
            ResponsesDict.Add("https://api-v3search-0.nuget.org/autocomplete?q=elm", JsonData.PsAutoCompleteV3Example);
            ResponsesDict.Add("https://nuget.org/api/v2/package-ids?partialId=elm", JsonData.PSAutoCompleteV2Example);
         }


        //[Theory]
        //[InlineData("http://testsource.com/v3/index.json")]
        //[InlineData("https://nuget.org/api/v2/")]
        public async Task PowershellAutoComplete_IdStartsWithReturnsExpectedResults(string sourceUrl)
        {
            // Arrange
            var source = StaticHttpHandler.CreateSource(sourceUrl, Repository.Provider.GetVisualStudio(), ResponsesDict);
            var resource = await source.GetResourceAsync<PSAutoCompleteResource>();
            Assert.NotNull(resource);

            // Act
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            IEnumerable<string> packages = await resource.IdStartsWith("elm", true, cancellationTokenSource.Token);

            // Assert
            Assert.True(packages != null & packages.Count() > 0);
            Assert.Contains("elmah", packages);
        }

        //[Theory]
        //[InlineData("http://testsource.com/v3/index.json")]
        //[InlineData("https://nuget.org/api/v2/")]
        public async Task PowershellAutoComplete_IdStartsWithCancelsAsAppropriate(string sourceUrl)
        {
            // Arrange
            var source = StaticHttpHandler.CreateSource(sourceUrl, Repository.Provider.GetVisualStudio(), ResponsesDict);
            var resource = await source.GetResourceAsync<PSAutoCompleteResource>();
            Assert.NotNull(resource);

            // Act
            CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
            Task<IEnumerable<string>> packagesTask = resource.IdStartsWith("elm", true, cancellationTokenSource.Token);
            cancellationTokenSource.Cancel();

            // Assert
            try
            {
                packagesTask.Wait();
            }
            catch (AggregateException e)
            {
                Assert.Equal(e.InnerExceptions.Count(), 1);
                Assert.True(e.InnerExceptions.Any(item => item.GetType().Equals(typeof(TaskCanceledException))));
            }
        }
    }
}
