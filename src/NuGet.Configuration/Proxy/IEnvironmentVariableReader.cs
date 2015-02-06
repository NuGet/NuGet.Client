namespace NuGet.Configuration
{
    public interface IEnvironmentVariableReader
    {
        string GetEnvironmentVariable(string variable);
    }
}