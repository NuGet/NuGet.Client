
namespace NuGet.Protocol.Core.Types
{
    /// <summary>
    /// Positions to base providers on
    /// </summary>
    public sealed class NuGetResourceProviderPositions
    {
        /// <summary>
        /// The first provider called
        /// </summary>
        public const string First = "First";

        /// <summary>
        /// The last provider called
        /// </summary>
        public const string Last = "Last";
    }
}
