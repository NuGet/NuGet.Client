using System;
using System.Threading.Tasks;
using NuGet.Common;


namespace NuGet.CommandLine.XPlat
{

    public partial class AddSourceArgs
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool StorePasswordInClearText { get; set; }
        public string ValidAuthenticationTypes { get; set; }
        public string Configfile { get; set; }
    }

    public partial class DisableSourceArgs
    {
        public string Name { get; set; }
        public string Configfile { get; set; }
    }

    public partial class EnableSourceArgs
    {
        public string Name { get; set; }
        public string Configfile { get; set; }
    }

    public partial class ListSourceArgs
    {
        public string Format { get; set; }
        public string Configfile { get; set; }
    }

    public partial class RemoveSourceArgs
    {
        public string Name { get; set; }
        public string Configfile { get; set; }
    }

    public partial class UpdateSourceArgs
    {
        public string Name { get; set; }
        public string Source { get; set; }
        public string Username { get; set; }
        public string Password { get; set; }
        public bool StorePasswordInClearText { get; set; }
        public string ValidAuthenticationTypes { get; set; }
        public string Configfile { get; set; }
    }

}
