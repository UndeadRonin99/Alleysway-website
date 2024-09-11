using Firebase.Auth;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using XBCAD.ViewModels;
using System.Net.Http.Headers;


namespace XBCAD.Controllers
{
    public class AdminController : Controller
    {
        public string userId;
        private readonly FirebaseAdmin.Auth.FirebaseAuth auth;
        private readonly HttpClient httpClient;
        public string uid;

        public async Task<IActionResult> Calendar()
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (!string.IsNullOrEmpty(accessToken))
            {
                var embedLink = await googleCalendarService.GetCalendarEmbedLinkAsync(accessToken);
                ViewBag.CalendarEmbedLink = embedLink;
            }
            else
            {
                ViewBag.CalendarEmbedLink = null;
            }

            return View();
        }


        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
            {
                return BadRequest("Google authentication failed.");
            }

            // Extract user info from Google authentication result
            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = authenticateResult.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = authenticateResult.Principal.FindFirst(ClaimTypes.Surname)?.Value;
            var googleUid = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;  // Google UID

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(lastName) || string.IsNullOrWhiteSpace(googleUid))
            {
                return BadRequest("Required user information is missing from Google account.");
            }

            try
            {
                UserRecord userRecord = null;

                // Check if user already exists in Firebase Auth using the Google UID
                try
                {
                    userRecord = await this.auth.GetUserAsync(googleUid);
                }
                catch (FirebaseAdmin.Auth.FirebaseAuthException ex)
                {
                    if (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                    {
                        // Create only if the user does not exist
                        userRecord = await this.auth.CreateUserAsync(new UserRecordArgs
                        {
                            Uid = googleUid,  // Set Firebase UID to Google UID
                            Email = email,
                            DisplayName = $"{firstName} {lastName}",
                            Disabled = false
                        });

                    }
                    else
                    {
                        throw;
                    }
                }

                // Fetch existing user data from Firebase RTDB
                var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";  // Use Google UID
                var existingDataResponse = await httpClient.GetStringAsync(url);

                // Use Dictionary instead of anonymous type to handle existing data
                var existingData = string.IsNullOrEmpty(existingDataResponse) || existingDataResponse == "null"
                    ? new Dictionary<string, dynamic>()  // Initialize empty dictionary
                    : JsonSerializer.Deserialize<Dictionary<string, dynamic>>(existingDataResponse);

                // Prepare new user data
                var data = new
                {
                    firstName = firstName,
                    lastName = lastName,
                    role = "admin"
                };

                // Merge new user data with existing data (preserving `Days` and other fields)
                foreach (var entry in data.GetType().GetProperties())
                {
                    existingData[entry.Name] = entry.GetValue(data);  // Update the existing data with new fields
                }

                // Serialize merged data and send to Firebase RTDB
                var json = JsonSerializer.Serialize(existingData);
                var content = new StringContent(json, Encoding.UTF8);
                content.Headers.ContentType = new MediaTypeHeaderValue("application/json");

                var response = await httpClient.PutAsync(url, content);
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception("Failed to update user data in the database.");
                }

            }
            catch (Exception ex)
            {
                return BadRequest($"Failed to update user in Firebase: {ex.Message}");
            }

            var name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = name;
            return View();
        }




        //testing to see if i have to revert
        public IActionResult Users()
        {
            ViewData["Title"] = "Manage Users";
            return View();
        }

        private readonly FirebaseService firebaseService;
        private readonly GoogleCalendarService googleCalendarService;

        public AdminController(IHttpClientFactory httpClientFactory, GoogleCalendarService calendarService)
        {
            firebaseService = new FirebaseService();
            googleCalendarService = calendarService;
            this.httpClient = httpClientFactory.CreateClient();

            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                });
            }

            this.auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
        }

        [HttpPost]
        public async Task<IActionResult> DeleteProfile()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Delete the user from Firebase Authentication
                await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.DeleteUserAsync(userId);

                // Optionally, you could also remove user data from the Realtime Database
                await firebaseService.DeleteUserDataAsync(userId);

                // Sign out the user after deletion
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                TempData["SuccessMessage"] = "Your profile has been deleted successfully.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Profile deletion failed: {ex.Message}");
                return RedirectToAction("Settings");
            }
        }

        [HttpPost]
        public async Task<IActionResult> ChangePassword(string oldPassword, string newPassword, string confirmNewPassword)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            if (newPassword != confirmNewPassword)
            {
                TempData["ErrorMessage"] = "New password and confirmation password do not match.";
                TempData["ActiveTab"] = "changePassword";
                return RedirectToAction("Settings");
            }

            try
            {
                // Re-authenticate the user with the old password
                var authRequest = new HttpRequestMessage(HttpMethod.Post, $"https://identitytoolkit.googleapis.com/v1/accounts:signInWithPassword?key=AIzaSyDX4j_urjkjhoxeN5AHFxcOW1viBqsicWA");
                var payload = new
                {
                    email = userEmail,
                    password = oldPassword,
                    returnSecureToken = true
                };

                authRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
                var response = await httpClient.SendAsync(authRequest);

                if (!response.IsSuccessStatusCode)
                {
                    TempData["ErrorMessage"] = "Old password is incorrect.";
                    TempData["ActiveTab"] = "changePassword";
                    return RedirectToAction("Settings");
                }

                // Update the password in Firebase Authentication
                await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.UpdateUserAsync(new UserRecordArgs
                {
                    Uid = userId,
                    Password = newPassword
                });

                TempData["SuccessMessage"] = "Password updated successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Password change failed: {ex.Message}";
            }

            TempData["ActiveTab"] = "changePassword";
            return RedirectToAction("Settings");
        }

        [HttpPost]
        public async Task<IActionResult> AddNewPT(string ptName, string ptEmail, string ptPassword, string ptConfirmPassword, string ptRate)
        {
            TempData["ActiveTab"] = "addPT"; // Set the active tab to "addPT"

            if (ptPassword != ptConfirmPassword)
            {
                ModelState.AddModelError("", "Password and Confirm Password do not match.");
                return RedirectToAction("Settings");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Create the new user in Firebase Authentication
                    var userRecord = await this.auth.CreateUserAsync(new UserRecordArgs
                    {
                        Email = ptEmail,
                        Password = ptPassword,
                        DisplayName = ptName,
                        Disabled = false,
                    });

                    string[] name = ptName.Split(" ");
                    string fName = name[0];
                    string sName = name[1];
                    // Prepare data to be saved in RTDB
                    var data = new
                    {
                        firstName = fName,
                        lastName = sName,
                        rate = ptRate,
                        role = "admin"
                    };
                    var json = JsonSerializer.Serialize(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Construct the URL to Firebase RTDB
                    var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{userRecord.Uid}.json";

                    // Send data to Firebase RTDB
                    var response = await httpClient.PutAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        TempData["SuccessMessage"] = "New PT added successfully.";
                        return RedirectToAction("Settings");
                    }
                    else
                    {
                        throw new Exception("Failed to save user data to database.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Error adding new PT: {ex.Message}");
                    return RedirectToAction("Settings");
                }
            }

            return RedirectToAction("Settings");
        }



        [HttpPost]
        public async Task<IActionResult> SaveProfile(IFormFile photo, string rate)
        {
            TempData["ActiveTab"] = "editProfile"; // Set the active tab to "addPT"

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
            Debug.WriteLine(userId);
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

            // Get the rate from Firebase Realtime Database
            var rate = await firebaseService.GetRateAsync(userId);
            ViewBag.Rate = rate;

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