namespace NuGet.Protocol
{
    public enum FeedType
    {
        Undefined = 0,
        HttpV2 = 1 << 0,
        HttpV3 = 1 << 1,
        FileSystemV2 = 1 << 2,
        FileSystemV3 = 1 << 3,
        FileSystemUnzipped = 1 << 4,
        FileSystemUnknown = 1 << 5
    }
}
