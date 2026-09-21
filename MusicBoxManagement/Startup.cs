using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(MusicBoxManagement.Startup))]
namespace MusicBoxManagement
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
        }
    }
}
