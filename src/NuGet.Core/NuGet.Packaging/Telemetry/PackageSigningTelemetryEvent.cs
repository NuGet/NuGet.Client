using System.Collections.Generic;
using NuGet.Common;

namespace NuGet.Packaging
{
    public class PackageSigningTelemetryEvent : TelemetryEvent
    {
        public PackageSignType PackageSignType => (PackageSignType)base[nameof(PackageSignType)];

        public NuGetOperationStatus Status => (NuGetOperationStatus)base[nameof(Status)];

        public string ExtractionId => (string)base[nameof(ExtractionId)];

        public const string EventName = "SigningInformation";

        public PackageSigningTelemetryEvent(PackageSignType packageSignType, NuGetOperationStatus status) :
            base(EventName, new Dictionary<string, object>
                {
                    { nameof(Status), status },
                    { nameof(PackageSignType), packageSignType }
                })
        { }
    }
}
