using Microsoft.AspNetCore.Mvc;
using XBCAD.ViewModels;
using System.Collections.Generic;

namespace XBCAD.Controllers
{
    public class AdminController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            return View();
        }

        public IActionResult Users()
        {
            ViewData["Title"] = "Manage Users";
            return View();
        }

        private readonly FirebaseService firebaseService;

        public AdminController()
        {
            firebaseService = new FirebaseService();
        }

        public async Task<IActionResult> Availability()
        {
            var model = await firebaseService.GetAvailabilityAsync();
            return View(model);
        }

        // Method to return updated availability as partial view
        public async Task<IActionResult> GetAvailabilityPartial()
        {
            var model = await firebaseService.GetAvailabilityAsync();
            return PartialView("_AvailabilityTablePartial", model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveTimeSlot(string day, string startTime, string endTime)
        {
            await firebaseService.SaveTimeSlotAsync(day, startTime, endTime);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTimeSlot(string day, string startTime, string endTime)
        {
            await firebaseService.RemoveTimeSlotAsync(day, startTime, endTime);
            return Json(new { success = true });
        }
    }
}