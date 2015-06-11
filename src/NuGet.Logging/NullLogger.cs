namespace NuGet.Logging
{
    public class NullLogger : ILogger
    {
        private static ILogger _instance;

        public static ILogger Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new NullLogger();
                }

                return _instance;
            }
        }

        public void LogDebug(string data) { }

        public void LogError(string data) { }

        public void LogInformation(string data) { }

        public void LogVerbose(string data) { }

        public void LogWarning(string data) { }
    }
}
