using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(SocialLife.Startup))]
namespace SocialLife
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
