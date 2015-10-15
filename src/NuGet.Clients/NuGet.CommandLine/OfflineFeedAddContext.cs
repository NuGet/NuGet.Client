using System;
using NuGet.ProjectManagement;

namespace NuGet.CommandLine
{
    public class OfflineFeedAddContext
    {
        public string PackagePath { get; }
        public string Source { get; }
        public Logging.ILogger Logger { get; }
        public bool ThrowIfSourcePackageIsInvalid { get; }
        public bool ThrowIfPackageExistsAndInvalid { get; }
        public bool ThrowIfPackageExists { get; }
        public bool Expand { get; }

        public OfflineFeedAddContext(
            string packagePath,
            string source,
            Logging.ILogger logger,
            bool throwIfSourcePackageIsInvalid,
            bool throwIfPackageExistsAndInvalid,
            bool throwIfPackageExists,
            bool expand)
        {
            if (string.IsNullOrEmpty(packagePath))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(packagePath));
            }

            if (string.IsNullOrEmpty(source))
            {
                throw new ArgumentException(Strings.Argument_Cannot_Be_Null_Or_Empty, nameof(source));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            PackagePath = packagePath;
            Source = source;
            Logger = logger;
            ThrowIfSourcePackageIsInvalid = throwIfSourcePackageIsInvalid;
            ThrowIfPackageExists = throwIfPackageExists;
            ThrowIfPackageExistsAndInvalid = throwIfPackageExistsAndInvalid;
            Expand = expand;
        }
    }
}
