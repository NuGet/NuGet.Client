using System.Collections.Generic;

namespace NuGet.Configuration
{
    public interface IMachineWideSettings
    {
        IEnumerable<Settings> Settings { get; }
    }
}
