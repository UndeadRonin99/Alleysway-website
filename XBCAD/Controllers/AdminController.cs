using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using XBCAD.ViewModels;


namespace XBCAD.Controllers
{
    public class AdminController : Controller
    {
        public string userId;
        private readonly FirebaseAdmin.Auth.FirebaseAuth auth;
        private readonly HttpClient httpClient;
        public string uid;
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

            this.auth = FirebaseAuth.DefaultInstance;
        }

        [HttpPost]
        public async Task<IActionResult> UpdateSessionPaymentStatus(string sessionId, bool isPaid, string clientId)
        {
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(trainerId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Update the session for the trainer
                await firebaseService.UpdateSessionPaymentStatusAsync(trainerId, sessionId, isPaid);

                // Update the session for the client
                await firebaseService.UpdateSessionPaymentStatusAsync(clientId, sessionId, isPaid);

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }


        public async Task<IActionResult> Income2(string id) // 'id' is the clientId
        {
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account"); // Redirect if user is not logged in
            }

            // Get client details
            var clientData = await firebaseService.GetClientByIdAsync(id);

            // Fetch and set the Base64 image
            if (!string.IsNullOrEmpty(clientData.ProfileImageUrl))
            {
                clientData.ProfileImageBase64 = await GetImageAsBase64Async(clientData.ProfileImageUrl);
            }

            // Get sessions between the trainer and the client
            var sessions = await firebaseService.GetSessionsBetweenTrainerAndClientAsync(trainerId, id);

            // Calculate total amounts
            decimal totalAmountDue = sessions.Where(s => !s.Paid).Sum(s => s.TotalAmount);
            decimal totalAmountPaid = sessions.Where(s => s.Paid).Sum(s => s.TotalAmount);

            // Prepare the ViewModel
            var model = new ClientSessionsViewModel
            {
                Client = clientData,
                Sessions = sessions,
                TotalAmountDue = totalAmountDue,
                TotalAmountPaid = totalAmountPaid
            };

            return View("Income2", model);
        }



        public async Task<IActionResult> Income()
        {
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account"); // Redirect if user is not logged in
            }

            var clients = await firebaseService.GetClientsForTrainerAsync(trainerId);
            ViewBag.Name = User.FindFirstValue(ClaimTypes.Name);
            return View(clients);
        }

        // In AdminController
        public async Task<IActionResult> Chat()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserId = userId;
            ViewBag.Name = name;

            // Get the list of clients the trainer has sessions with
            var clientsFromSessions = await firebaseService.GetClientsForTrainerAsync(userId);

            // Get the list of user IDs from message history
            var messageContactIds = await firebaseService.GetMessageContactsAsync(userId);

            // Fetch client details for these message contacts
            var clientsFromMessages = await firebaseService.GetClientsByIdsAsync(messageContactIds);

            // Merge the two lists, removing duplicates
            var allClients = clientsFromSessions.Concat(clientsFromMessages)
                .GroupBy(c => c.Id)
                .Select(g => g.First())
                .ToList();

            ViewBag.Contacts = allClients;

            // Generate a custom Firebase Auth token
            var firebaseToken = await GenerateFirebaseTokenAsync(userId);
            ViewBag.FirebaseToken = firebaseToken;

            return View();
        }




        private async Task<string> GenerateFirebaseTokenAsync(string userId)
        {
            // Initialize Firebase Admin SDK if not already done
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                });
            }

            // Generate a custom token for the user
            string customToken = await FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(userId);
            return customToken;
        }

        public async Task<IActionResult> Calendar()
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            var email = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.Name = Name;

            if (!string.IsNullOrEmpty(accessToken))
            {
                var embedLink = await googleCalendarService.GetCalendarEmbedLinkAsync(accessToken, email);
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

            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(firstName) || string.IsNullOrWhiteSpace(googleUid))
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

        public IActionResult Users()
        {
            ViewData["Title"] = "Manage Users";
            return View();
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

        public string EncodeEmail(string email)
        {
            return email.Replace('.', ',');
        }

        [HttpPost]
        public async Task<IActionResult> AddNewPT(string ptName, string ptEmail, string ptRate)
        {
            TempData["ActiveTab"] = "addPT"; // Set the active tab to "addPT"

            if (ModelState.IsValid)
            {
                try
                {
                    string[] nameParts = ptName.Split(' ');
                    string firstName = nameParts[0];
                    string lastName = nameParts.Length > 1 ? nameParts[1] : "";

                    var data = new
                    {
                        firstName = firstName,
                        lastName = lastName,
                        role = "admin",
                        ptRate = ptRate,
                        status = "pending"
                    };

                    var json = JsonSerializer.Serialize(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/pending_users/{EncodeEmail(ptEmail)}.json";
                    var response = await httpClient.PutAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        TempData["SuccessMessage"] = "New PT setup initiated. Complete registration through Google login.";
                        return RedirectToAction("Settings");
                    }
                    else
                    {
                        var responseBody = await response.Content.ReadAsStringAsync();
                        throw new Exception($"Failed to initialize user setup in database. Response: {responseBody}");
                    }
                }
                catch (Exception ex)
                {
                    TempData["FailMessage"] = "PT setup failed please try again";
                    ModelState.AddModelError("", $"Error adding new PT: {ex.Message}");
                    return RedirectToAction("Settings");
                }
            }

            return RedirectToAction("Settings");
        }


        [HttpPost]
        public async Task<IActionResult> SaveProfile(IFormFile photo, string rate)
        {
            TempData["ActiveTab"] = "editProfile"; // Set the active tab to "editProfile"
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Validate rate to ensure it's an integer
                if (!int.TryParse(rate, out int parsedRate))
                {
                    ModelState.AddModelError("Rate", "The rate must be a numeric value.");
                    return RedirectToAction("Settings");
                }

                string imageUrl = null;

                // If a photo was uploaded, upload it to Firebase Storage
                if (photo != null && photo.Length > 0)
                {
                    imageUrl = await firebaseService.UploadProfileImageAsync(userId, photo);

                    // Save the image URL to Firebase Realtime Database
                    await firebaseService.SaveProfileImageUrlAsync(userId, imageUrl);
                }

                // Save the rate as an integer to Firebase Realtime Database
                await firebaseService.SaveRateAsync(userId, parsedRate.ToString());

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
            var name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = name;
            ViewBag.Email = email;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account"); // Redirect to login if the user ID is not found
            }

            // Get the profile image URL from Firebase Realtime Database
            var profileImageUrl = await firebaseService.GetProfileImageUrlAsync(userId);

            // Set a default image if profileImageUrl is null or empty
            ViewBag.ProfileImageUrl = !string.IsNullOrEmpty(profileImageUrl)
                ? profileImageUrl
                : "/images/default.jpg"; // Path relative to the wwwroot folder

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
                return RedirectToAction("Login", "Account");
            }

            var model = new AvailabilityViewModel
            {
                UserId = userId,
                Days = await firebaseService.GetAllAvailabilityAsync(userId),  // Fetches default availability
                DateSpecificAvailability = await firebaseService.GetAllDateSpecificAvailabilityAsync(userId) // Fetches date-specific availability
            };

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

        [HttpPost]
        public async Task<IActionResult> SaveDateSpecificTimeSlot(string date, string startTime, string endTime)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            try
            {
                // Call the service and get the slotId of the saved availability
                var slotId = await firebaseService.SaveDateSpecificAvailabilityAsync(userId, date, startTime, endTime);
                return Json(new { success = true, message = "Date-specific availability saved successfully.", slotId = slotId });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving date-specific availability: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving date-specific availability. Please try again later." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> SaveDateSpecificUnavailability(string startDate, string endDate)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            try
            {
                DateTime start = DateTime.Parse(startDate);
                DateTime end = DateTime.Parse(endDate);

                if (end < start)
                {
                    return Json(new { success = false, message = "End date cannot be before start date." });
                }

                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var dateString = date.ToString("yyyy-MM-dd");
                    await firebaseService.SaveDateSpecificUnavailabilityAsync(userId, dateString, null, null, true);
                }

                return Json(new { success = true, message = "Date-specific unavailability saved successfully." });
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error saving date-specific unavailability: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving date-specific unavailability. Please try again later." });
            }
        }



        [HttpGet]
        public async Task<IActionResult> GetDateSpecificAvailability(string userId)
        {
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            var availability = await firebaseService.GetAllDateSpecificAvailabilityAsync(userId);
            if (availability != null)
            {
                return Json(new { success = true, availability });
            }
            return Json(new { success = false, message = "Failed to load date-specific availability." });
        }

        [HttpPost]
        public async Task<IActionResult> RemoveDateSpecificAvailability(string date, string slotId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(slotId))
            {
                return Json(new { success = false, message = "Invalid data provided." });
            }

            var result = await firebaseService.RemoveDateSpecificTimeSlotAsync(userId, date, slotId);

            if (result)
            {
                return Json(new { success = true });
            }
            else
            {
                return Json(new { success = false, message = "Failed to delete availability." });
            }
        }
        public async Task<IActionResult> PaymentOverview()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;

            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessions = await firebaseService.GetAllSessionsForTrainerAsync(trainerId);

            var model = new PaymentOverviewViewModel
            {
                Sessions = sessions // Assigning the correct type here
            };

            return View("PaymentOverview", model);
        }

        private async Task<string> GetImageAsBase64Async(string imageUrl)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    var imageBytes = await client.GetByteArrayAsync(imageUrl);
                    return Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    // Handle exceptions (e.g., image not found)
                    Console.WriteLine($"Error fetching image: {ex.Message}");
                    return null;
                }
            }
        }



    }
}