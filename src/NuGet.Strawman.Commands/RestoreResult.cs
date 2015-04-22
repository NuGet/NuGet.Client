namespace NuGet.Strawman.Commands
{
    public class RestoreResult
    {
        public bool Success { get; }

        public RestoreResult(bool success)
        {
            Success = success;
        }
    }
}