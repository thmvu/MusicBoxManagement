using System;
using System.Data.Entity.Infrastructure;
using System.Linq;
using System.Web.Mvc;
using Microsoft.AspNet.Identity;
using MusicBoxManagement.Authorization;
using MusicBoxManagement.Models;
using MusicBoxManagement.Services;

namespace MusicBoxManagement.Controllers
{
    [PermissionAuthorize("Room.Manage")]
    public class RoomsController : Controller
    {
        public ActionResult Index()
        {
            using (var db = new ApplicationDbContext())
                return View(new RoomService(db, ImageDirectory()).List());
        }

        public ActionResult Create()
        {
            using (var db = new ApplicationDbContext()) SetRoomTypes(db);
            return View(new RoomFormViewModel());
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Create(RoomFormViewModel model)
        {
            using (var db = new ApplicationDbContext())
            {
                var service = new RoomService(db, ImageDirectory());
                Validate(model, service, true);
                if (ModelState.IsValid)
                {
                    try
                    {
                        service.Create(model, User.Identity.GetUserId());
                        TempData["Success"] = "Đã tạo phòng.";
                        return RedirectToAction("Index");
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("RoomCode", "Mã phòng đã tồn tại hoặc dữ liệu không hợp lệ."); }
                }
                SetRoomTypes(db);
                return View(model);
            }
        }

        public ActionResult Edit(int id)
        {
            using (var db = new ApplicationDbContext())
            {
                var model = new RoomService(db, ImageDirectory()).GetForm(id);
                if (model == null) return HttpNotFound();
                SetRoomTypes(db);
                return View(model);
            }
        }

        [HttpPost, ValidateAntiForgeryToken]
        public ActionResult Edit(RoomFormViewModel model)
        {
            using (var db = new ApplicationDbContext())
            {
                var service = new RoomService(db, ImageDirectory());
                var current = service.GetForm(model.RoomId);
                if (current == null) return HttpNotFound();
                model.RoomCode = current.RoomCode;
                model.ImageUrl = current.ImageUrl;
                ModelState.Remove("RoomCode");
                Validate(model, service, false);
                if (ModelState.IsValid)
                {
                    try
                    {
                        service.Update(model, User.Identity.GetUserId());
                        TempData["Success"] = "Đã cập nhật phòng.";
                        return RedirectToAction("Index");
                    }
                    catch (ArgumentException error) { ModelState.AddModelError("", error.Message); }
                    catch (DbUpdateException) { ModelState.AddModelError("", "Không thể cập nhật phòng. Vui lòng thử lại."); }
                }
                SetRoomTypes(db);
                return View(model);
            }
        }

        private void Validate(RoomFormViewModel model, RoomService service, bool creating)
        {
            if (creating)
            {
                if (string.IsNullOrWhiteSpace(model.RoomCode)) ModelState.AddModelError("RoomCode", "Mã phòng là bắt buộc.");
                else if (service.RoomCodeExists(model.RoomCode)) ModelState.AddModelError("RoomCode", "Mã phòng đã tồn tại.");
                if (model.Image == null || model.Image.ContentLength == 0) ModelState.AddModelError("Image", "Cần chọn một ảnh phòng.");
            }
            if (string.IsNullOrWhiteSpace(model.Name)) ModelState.AddModelError("Name", "Tên phòng là bắt buộc.");
            if (!service.RoomTypeExists(model.RoomTypeId)) ModelState.AddModelError("RoomTypeId", "Loại phòng không tồn tại.");
        }

        private void SetRoomTypes(ApplicationDbContext db)
        {
            ViewBag.RoomTypes = new SelectList(db.RoomTypes.OrderBy(roomType => roomType.Code).ToList(), "RoomTypeId", "Name");
        }

        private string ImageDirectory()
        {
            return Server.MapPath("~/Content/uploads/rooms");
        }
    }
}
