using Microsoft.AspNetCore.Mvc;
using XBCAD.ViewModels;
using System.Collections.Generic;
using System.Security.Claims;

namespace XBCAD.Controllers
{
    public class AdminController : Controller
    {
        public string userId;
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;
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

        [HttpPost]
        public async Task<IActionResult> SaveProfile(IFormFile photo, string rate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                string imageUrl = null;


                // If a photo was uploaded, upload it to Firebase Storage
                if (photo != null && photo.Length > 0)
                {
                    imageUrl = await firebaseService.UploadProfileImageAsync(userId, photo);

                    
                    // Save the image URL to Firebase Realtime Database
                    await firebaseService.SaveProfileImageUrlAsync(userId, imageUrl);
                }

                // Save the rate to Firebase Realtime Database
                await firebaseService.SaveRateAsync(userId, rate);


                TempData["SuccessMessage"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Profile update failed: {ex.Message}");
            }

            return RedirectToAction("Settings");
        }


        

        public async Task<IActionResult> Settings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;
            ViewBag.Email = email;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account"); // Redirect to login if the user ID is not found
            }

            // Get the profile image URL from Firebase Realtime Database
            var profileImageUrl = await firebaseService.GetProfileImageUrlAsync(userId);
            ViewBag.ProfileImageUrl = profileImageUrl;

            var model = await firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;
            return View(model); // Pass the availability data to the view
        }

        public async Task<IActionResult> Availability()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account"); // Redirect to login if the user ID is not found
            }

            var model = await firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;
            return View(model); // Pass the availability data to the view
        }


        // Method to return updated availability as partial view
        public async Task<IActionResult> GetAvailabilityPartial()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (string.IsNullOrEmpty(userId))
            {
                // Handle the case where userId is not provided
                return PartialView("_AvailabilityTablePartial", new AvailabilityViewModel()); // Return empty view model
            }

            var model = await firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;
            return PartialView("_AvailabilityTablePartial", model);
        }

        [HttpPost]
        public async Task<IActionResult> SaveTimeSlot(string day, string startTime, string endTime)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            await firebaseService.SaveTimeSlotAsync(day, startTime, endTime, userId);
            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveTimeSlot(string day, string startTime, string endTime)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(day) || string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                return Json(new { success = false, message = "Invalid parameters." });
            }

            try
            {
                await firebaseService.RemoveTimeSlotAsync(day, startTime, endTime, userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }
    }
}