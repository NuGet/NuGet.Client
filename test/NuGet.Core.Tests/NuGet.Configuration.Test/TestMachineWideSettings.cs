using System.Collections.Generic;

namespace NuGet.Configuration.Test
{
    public class TestMachineWideSettings : IMachineWideSettings
    {
        public IEnumerable<Settings> Settings { get; }

        public TestMachineWideSettings(Settings settings)
        {
            Settings = new List<Settings>() { settings };
        }
    }
}
