using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;
using NuGet.Common;
using NuGet.Packaging.Core;
using NuGet.Protocol.Core.Types;

namespace NuGet.Protocol
{
    public class DownloadResourceV2Feed : DownloadResource
    {
        private readonly V2FeedParser _feedParser;

        public DownloadResourceV2Feed(V2FeedParser feedParser)
        {
            if (feedParser == null)
            {
                throw new ArgumentNullException(nameof(feedParser));
            }

            _feedParser = feedParser;
        }

        public override async Task<DownloadResourceResult> GetDownloadResourceResultAsync(
            PackageIdentity identity,
            PackageDownloadContext downloadContext,
            string globalPackagesFolder,
            ILogger logger,
            CancellationToken token)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (downloadContext == null)
            {
                throw new ArgumentNullException(nameof(downloadContext));
            }

            if (logger == null)
            {
                throw new ArgumentNullException(nameof(logger));
            }

            token.ThrowIfCancellationRequested();

            var sourcePackage = identity as SourcePackageDependencyInfo;
            bool isFromUri = sourcePackage?.PackageHash != null
                            && sourcePackage?.DownloadUri != null;

            try
            {
                if (isFromUri)
                {
                    // If this is a SourcePackageDependencyInfo object with everything populated
                    // and it is from an online source, use the machine cache and download it using the
                    // given url.
                    return await _feedParser.DownloadFromUrl(
                        sourcePackage,
                        sourcePackage.DownloadUri,
                        downloadContext,
                        globalPackagesFolder,
                        logger,
                        token);
                }
                else
                {
                    // Look up the package from the id and version and download it.
                    return await _feedParser.DownloadFromIdentity(
                        identity,
                        downloadContext,
                        globalPackagesFolder,
                        logger,
                        token);
                }
            }
            catch (OperationCanceledException)
            {
                return new DownloadResourceResult(DownloadResourceResultStatus.Cancelled);
            }
            catch (Exception ex) when (!(ex is FatalProtocolException))
            {
                // if the expcetion is not FatalProtocolException, catch it.
                string message = string.Format(CultureInfo.CurrentCulture, Strings.Log_ErrorDownloading, identity, _feedParser.Source.Source);
                logger.LogError(message + Environment.NewLine + ExceptionUtilities.DisplayMessage(ex));

                throw new FatalProtocolException(message, ex);
            }
        }
    }
}
