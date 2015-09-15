using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof($safeprojectname$.Startup))]
namespace $safeprojectname$
{
    public partial class Startup {
        public void Configuration(IAppBuilder app) {
            ConfigureAuth(app);
        }
    }
}
