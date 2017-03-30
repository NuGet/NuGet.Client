namespace NuGet.Test.Utility
{
    public static class TestServers
    {
        public const string Artifactory = @"http://artifactory:8081/artifactory/api/nuget/nuget";
        public const string Klondike = @"http://klondikeserver:8081/api/odata";
        public const string MyGet = @"https://dotnet.myget.org/F/nuget-myget-integration-tests/api/v2";
        public const string Nexus = @"http://nexusservertest:8081/nexus/service/local/nuget/NuGet";
        public const string NuGetServer = @"http://nugetserverendpoint.azurewebsites.net/nuget";
        public const string NuGetV2 = @"https://www.nuget.org/api/v2";
        public const string ProGet = @"http://progetserver:8081/nuget/nuget";
        public const string TeamCity = @"http://teamcityserver:8081/guestAuth/app/nuget/v1/FeedService.svc";
        public const string Vsts = @"https://vstsnugettest.pkgs.visualstudio.com/DefaultCollection/_packaging/VstsTestFeed/nuget/v2";
    }
}
