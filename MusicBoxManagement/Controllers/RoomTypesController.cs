using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("RoomType.Edit")]
    public class RoomTypesController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new RoomTypeService(db).List());
        }

        public ActionResult Edit(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = new RoomTypeService(db).GetEdit(id);
                if (model == null) return HttpNotFound();
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(EditRoomTypeViewModel model)
        {
            using (var db = new ApplicationDbContext())
            {
                var service = new RoomTypeService(db);
                var current = service.GetEdit(model.RoomTypeId);
                if (current == null) return HttpNotFound();
                model.Code = current.Code;

                if (!ModelState.IsValid) return View(model);
                if (string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError("Name", "Tên loại phòng là bắt buộc.");
                if (string.IsNullOrWhiteSpace(model.Amenities)) ModelState.AddModelError("Amenities", "Tiện ích là bắt buộc.");
                if (model.PricePerHour != decimal.Truncate(model.PricePerHour)) ModelState.AddModelError("PricePerHour", "Giá theo giờ phải là số nguyên đồng.");
                if (!ModelState.IsValid) return View(model);

                if (!service.Update(model, User.Identity.GetUserId())) return new HttpStatusCodeResult(403);
                TempData["Success"] = "Đã cập nhật loại phòng.";
                return RedirectToAction("Index");
            }
        }

    }
}
