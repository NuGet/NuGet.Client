namespace NuGet.Packaging
{
    public static class PathUtility
    {
        public static string GetPathWithForwardSlashes(string path)
        {
            return path.Replace('\\', '/');
        }
    }
}