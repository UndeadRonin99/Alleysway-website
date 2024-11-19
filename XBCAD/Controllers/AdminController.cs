// Required namespaces for Firebase, Google Authentication, MVC, etc.
using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using XBCAD.ViewModels;

namespace XBCAD.Controllers
{
    // Controller for administrative actions
    public class AdminController : Controller
    {
        // Public field to store the user ID
        public string userId;
        // Firebase authentication instance
        private readonly FirebaseAdmin.Auth.FirebaseAuth auth;
        // HTTP client for making HTTP requests
        private readonly HttpClient httpClient;
        // UID of the user
        public string uid;
        // Firebase service for database operations
        private readonly FirebaseService firebaseService;
        // Service for Google Calendar interactions
        private readonly GoogleCalendarService googleCalendarService;

        // Constructor for AdminController
        public AdminController(IHttpClientFactory httpClientFactory, GoogleCalendarService calendarService)
        {
            // Initialize Firebase and Google Calendar services
            firebaseService = new FirebaseService();
            googleCalendarService = calendarService;
            this.httpClient = httpClientFactory.CreateClient();

            // Initialize Firebase App if it hasn't been already
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                });
            }

            // Set the FirebaseAuth instance
            this.auth = FirebaseAuth.DefaultInstance;
        }

        // Endpoint to update the payment status of a session
        [HttpPost]
        public async Task<IActionResult> UpdateSessionPaymentStatus(string sessionId, bool isPaid, string clientId)
        {
            // Retrieve trainer ID from the user's claims
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if the trainer is authenticated
            if (string.IsNullOrEmpty(trainerId))
            {
                return Json(new { success = false, message = "User not authenticated" });
            }

            try
            {
                // Update the session payment status for the trainer
                await firebaseService.UpdateSessionPaymentStatusAsync(trainerId, sessionId, isPaid);

                // Update the session payment status for the client
                await firebaseService.UpdateSessionPaymentStatusAsync(clientId, sessionId, isPaid);

                // Return success response
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Return error response with exception message
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Action to display income details for a specific client
        public async Task<IActionResult> Income2(string id) // 'id' is the clientId
        {
            // Retrieve trainer ID and name from claims
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            // Check if the trainer is authenticated
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account"); // Redirect if user is not logged in
            }

            // Get client details
            var clientData = await firebaseService.GetClientByIdAsync(id);

            // Fetch and set the Base64 image if profile image URL exists
            if (!string.IsNullOrEmpty(clientData.ProfileImageUrl))
            {
                clientData.ProfileImageBase64 = await GetImageAsBase64Async(clientData.ProfileImageUrl);
            }

            // Get sessions between the trainer and the client
            var sessions = await firebaseService.GetSessionsBetweenTrainerAndClientAsync(trainerId, id);

            // Calculate total amounts due and paid
            decimal totalAmountDue = sessions.Where(s => !s.Paid).Sum(s => s.TotalAmount);
            decimal totalAmountPaid = sessions.Where(s => s.Paid).Sum(s => s.TotalAmount);

            // Prepare the ViewModel with client and session data
            var model = new ClientSessionsViewModel
            {
                Client = clientData,
                Sessions = sessions,
                TotalAmountDue = totalAmountDue,
                TotalAmountPaid = totalAmountPaid
            };

            // Return the Income2 view with the model
            return View("Income2", model);
        }

        // Action to display income overview
        public async Task<IActionResult> Income()
        {
            // Retrieve trainer ID and name from claims
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            // Check if the trainer is authenticated
            if (string.IsNullOrEmpty(trainerId))
            {
                return RedirectToAction("Login", "Account"); // Redirect if user is not logged in
            }

            // Get the list of clients associated with the trainer
            var clients = await firebaseService.GetClientsForTrainerAsync(trainerId);
            ViewBag.Name = User.FindFirstValue(ClaimTypes.Name);
            // Return the view with the clients list
            return View(clients);
        } 

        // Action for chat functionality
        public async Task<IActionResult> Chat()
        {
            // Retrieve user ID and name from claims
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

            // Pass the contacts to the view
            ViewBag.Contacts = allClients;

            // Generate a custom Firebase Auth token
            var firebaseToken = await GenerateFirebaseTokenAsync(userId);
            ViewBag.FirebaseToken = firebaseToken;

            // Return the Chat view
            return View();
        }

        // Private method to generate a custom Firebase token
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

        // Action to display the calendar
        public async Task<IActionResult> Calendar()
        {
            // Retrieve the access token from the context
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            var email = User.FindFirstValue(ClaimTypes.Email);
            ViewBag.Name = Name;

            // Check if access token is available
            if (!string.IsNullOrEmpty(accessToken))
            {
                // Get the calendar embed link
                var embedLink = await googleCalendarService.GetCalendarEmbedLinkAsync(accessToken, email);
                ViewBag.CalendarEmbedLink = embedLink;
            }
            else
            {
                ViewBag.CalendarEmbedLink = null;
            }

            // Return the Calendar view
            return View();
        }

        // Action to display the admin dashboard
        public async Task<IActionResult> Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            // Authenticate using Google defaults
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            // Check if authentication succeeded
            if (!authenticateResult.Succeeded)
            {
                return BadRequest("Google authentication failed.");
            }

            // Extract user info from Google authentication result
            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = authenticateResult.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = authenticateResult.Principal.FindFirst(ClaimTypes.Surname)?.Value;
            var googleUid = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;  // Google UID

            // Validate extracted information
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
                        // Create a new user if not found
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

                // Fetch existing user data from Firebase Realtime Database
                var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";  // Use Google UID
                var existingDataResponse = await httpClient.GetStringAsync(url);

                // Deserialize existing data or initialize a new dictionary
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

                // Merge new user data with existing data (preserving other fields)
                foreach (var entry in data.GetType().GetProperties())
                {
                    existingData[entry.Name] = entry.GetValue(data);  // Update the existing data with new fields
                }

                // Serialize merged data and send to Firebase Realtime Database
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

            // Retrieve Name from claims and pass to the view
            var name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = name;
            // Return the Dashboard view
            return View();
        }

        // Action to manage users
        public IActionResult Users()
        {
            ViewData["Title"] = "Manage Users";
            return View();
        }

        // Action to delete the user's profile
        [HttpPost]
        public async Task<IActionResult> DeleteProfile()
        {
            // Retrieve user ID from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user is authenticated
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            try
            {
                // Delete the user from Firebase Authentication
                await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.DeleteUserAsync(userId);

                // Optionally, remove user data from the Realtime Database
                await firebaseService.DeleteUserDataAsync(userId);

                // Sign out the user after deletion
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                // Inform the user about successful deletion
                TempData["SuccessMessage"] = "Your profile has been deleted successfully.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                // Handle any errors during deletion
                ModelState.AddModelError("", $"Profile deletion failed: {ex.Message}");
                return RedirectToAction("Settings");
            }
        }

        // Utility method to encode email addresses for Firebase paths
        public string EncodeEmail(string email)
        {
            return email.Replace('.', ',');
        }

        // Action to add a new Personal Trainer (PT)
        [HttpPost]
        public async Task<IActionResult> AddNewPT(string ptName, string ptEmail, string ptRate)
        {
            TempData["ActiveTab"] = "addPT"; // Set the active tab to "addPT"

            // Validate the model state
            if (ModelState.IsValid)
            {
                try
                {
                    // Split the PT's name into first and last names
                    string[] nameParts = ptName.Split(' ');
                    string firstName = nameParts[0];
                    string lastName = nameParts.Length > 1 ? nameParts[1] : "";

                    // Prepare data to be stored
                    var data = new
                    {
                        firstName = firstName,
                        lastName = lastName,
                        role = "admin",
                        ptRate = ptRate,
                        status = "pending"
                    };

                    // Serialize the data and send to Firebase Realtime Database
                    var json = JsonSerializer.Serialize(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/pending_users/{EncodeEmail(ptEmail)}.json";
                    var response = await httpClient.PutAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        // Inform about successful initiation
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
                    // Handle errors during PT addition
                    TempData["FailMessage"] = "PT setup failed please try again";
                    ModelState.AddModelError("", $"Error adding new PT: {ex.Message}");
                    return RedirectToAction("Settings");
                }
            }

            return RedirectToAction("Settings");
        }

        // Action to save profile changes
        [HttpPost]
        public async Task<IActionResult> SaveProfile(IFormFile photo, string rate)
        {
            TempData["ActiveTab"] = "editProfile"; // Set the active tab to "editProfile"
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                // Validate rate to ensure it's a numeric value
                if (!int.TryParse(rate, out int parsedRate))
                {
                    ModelState.AddModelError("Rate", "The rate must be a numeric value.");
                    return RedirectToAction("Settings");
                }

                string imageUrl = null;

                // If a photo was uploaded, handle the upload
                if (photo != null && photo.Length > 0)
                {
                    // Upload the photo to Firebase Storage
                    imageUrl = await firebaseService.UploadProfileImageAsync(userId, photo);

                    // Save the image URL to Firebase Realtime Database
                    await firebaseService.SaveProfileImageUrlAsync(userId, imageUrl);
                }

                // Save the rate to Firebase Realtime Database
                await firebaseService.SaveRateAsync(userId, parsedRate.ToString());

                // Inform about successful profile update
                TempData["SuccessMessage"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                // Handle errors during profile update
                ModelState.AddModelError("", $"Profile update failed: {ex.Message}");
            }

            return RedirectToAction("Settings");
        }

        // Action to display settings
        public async Task<IActionResult> Settings()
        {
            // Retrieve user ID, email, and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = name;
            ViewBag.Email = email;

            // Check if user is authenticated
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

            // Get availability data
            var model = await firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;

            // Pass the availability data to the view
            return View(model);
        }

        // Action to display availability settings
        public async Task<IActionResult> Availability()
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Check if user is authenticated
            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account");
            }

            // Prepare the AvailabilityViewModel with default and date-specific availability
            var model = new AvailabilityViewModel
            {
                UserId = userId,
                Days = await firebaseService.GetAllAvailabilityAsync(userId),  // Fetches default availability
                DateSpecificAvailability = await firebaseService.GetAllDateSpecificAvailabilityAsync(userId) // Fetches date-specific availability
            };

            // Pass the availability data to the view
            return View(model);
        }

        // Method to return updated availability as partial view
        public async Task<IActionResult> GetAvailabilityPartial()
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Check if user ID is provided
            if (string.IsNullOrEmpty(userId))
            {
                // Return empty view model if user ID is missing
                return PartialView("_AvailabilityTablePartial", new AvailabilityViewModel());
            }

            // Get the availability data
            var model = await firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;

            // Return the partial view with the model
            return PartialView("_AvailabilityTablePartial", model);
        }

        // Action to save a time slot
        [HttpPost]
        public async Task<IActionResult> SaveTimeSlot(string day, string startTime, string endTime)
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Check if user ID is provided
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            // Save the time slot using Firebase service
            await firebaseService.SaveTimeSlotAsync(day, startTime, endTime, userId);
            return Json(new { success = true });
        }

        // Action to remove a time slot
        [HttpPost]
        public async Task<IActionResult> RemoveTimeSlot(string day, string startTime, string endTime)
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Validate parameters
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(day) || string.IsNullOrEmpty(startTime) || string.IsNullOrEmpty(endTime))
            {
                return Json(new { success = false, message = "Invalid parameters." });
            }

            try
            {
                // Remove the time slot using Firebase service
                await firebaseService.RemoveTimeSlotAsync(day, startTime, endTime, userId);
                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Handle errors during removal
                return Json(new { success = false, message = ex.Message });
            }
        }

        // Action to save date-specific availability
        [HttpPost]
        public async Task<IActionResult> SaveDateSpecificTimeSlot(string date, string startTime, string endTime)
        {
            // Retrieve user ID from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user ID is provided
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            try
            {
                // Save date-specific availability and get the slot ID
                var slotId = await firebaseService.SaveDateSpecificAvailabilityAsync(userId, date, startTime, endTime);
                return Json(new { success = true, message = "Date-specific availability saved successfully.", slotId = slotId });
            }
            catch (Exception ex)
            {
                // Handle errors during saving
                Console.WriteLine($"Error saving date-specific availability: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving date-specific availability. Please try again later." });
            }
        }

        // Action to save date-specific unavailability
        [HttpPost]
        public async Task<IActionResult> SaveDateSpecificUnavailability(string startDate, string endDate)
        {
            // Retrieve user ID from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            // Check if user ID is provided
            if (string.IsNullOrEmpty(userId))
            {
                return Json(new { success = false, message = "User ID is required." });
            }

            try
            {
                // Parse start and end dates
                DateTime start = DateTime.Parse(startDate);
                DateTime end = DateTime.Parse(endDate);

                // Validate date range
                if (end < start)
                {
                    return Json(new { success = false, message = "End date cannot be before start date." });
                }

                // Loop through each date in the range and save unavailability
                for (var date = start; date <= end; date = date.AddDays(1))
                {
                    var dateString = date.ToString("yyyy-MM-dd");
                    await firebaseService.SaveDateSpecificUnavailabilityAsync(userId, dateString, null, null, true);
                }

                // Inform about successful saving
                return Json(new { success = true, message = "Date-specific unavailability saved successfully." });
            }
            catch (Exception ex)
            {
                // Handle errors during saving
                Console.WriteLine($"Error saving date-specific unavailability: {ex.Message}");
                return Json(new { success = false, message = "An error occurred while saving date-specific unavailability. Please try again later." });
            }
        }

        // Action to get date-specific availability
        [HttpGet]
        public async Task<IActionResult> GetDateSpecificAvailability(string userId)
        {
            // Retrieve Name from claims
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Get the availability data
            var availability = await firebaseService.GetAllDateSpecificAvailabilityAsync(userId);
            if (availability != null)
            {
                return Json(new { success = true, availability });
            }
            // Handle case where availability is null
            return Json(new { success = false, message = "Failed to load date-specific availability." });
        }

        // Action to remove date-specific availability
        [HttpPost]
        public async Task<IActionResult> RemoveDateSpecificAvailability(string date, string slotId)
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Validate parameters
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(date) || string.IsNullOrEmpty(slotId))
            {
                return Json(new { success = false, message = "Invalid data provided." });
            }

            // Attempt to remove the date-specific time slot
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

        // Action to display payment overview
        public async Task<IActionResult> PaymentOverview()
        {
            // Retrieve user ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = Name;

            // Get all sessions for the trainer
            var trainerId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var sessions = await firebaseService.GetAllSessionsForTrainerAsync(trainerId);

            // Prepare the PaymentOverviewViewModel
            var model = new PaymentOverviewViewModel
            {
                Sessions = sessions // Assigning the correct type here
            };

            // Return the PaymentOverview view with the model
            return View("PaymentOverview", model);
        }

        // Private method to convert image URL to Base64 string
        private async Task<string> GetImageAsBase64Async(string imageUrl)
        {
            using (var client = new HttpClient())
            {
                try
                {
                    // Fetch the image bytes from the URL
                    var imageBytes = await client.GetByteArrayAsync(imageUrl);
                    // Convert the bytes to a Base64 string
                    return Convert.ToBase64String(imageBytes);
                }
                catch (Exception ex)
                {
                    // Handle exceptions, e.g., image not found
                    Console.WriteLine($"Error fetching image: {ex.Message}");
                    return null;
                }
            }
        }
    }
}
