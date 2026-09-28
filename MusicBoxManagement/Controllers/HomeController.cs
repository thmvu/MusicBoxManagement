using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            if (Request.IsAuthenticated)
            {
                using (var db = new ApplicationDbContext())
                    ViewBag.CanDashboard = new PermissionService(db)
                        .HasPermission(User.Identity.GetUserId(), "Dashboard.View");
            }
            return View();
        }

        public ActionResult About()
        {
            return View();
        }

        public ActionResult Contact()
        {
            return View();
        }
    }
}