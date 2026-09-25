using System.Web.Mvc;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    public class PublicRoomsController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new PublicRoomCatalogService(db).List());
        }

        public ActionResult Details(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var room = new PublicRoomCatalogService(db).Get(id);
                return room == null ? (ActionResult)HttpNotFound() : View(room);
            }
        }
    }
}
