using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(WebAppTest.Startup))]
namespace WebAppTest
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
