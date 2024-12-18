// Import statements for Firebase Authentication, Google APIs, MVC framework, etc.
using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Calendar.v3.Data;
using Google.Apis.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using XBCAD.ViewModels;


namespace XBCAD.Controllers
{
    // Controller class for client-related actions
    public class ClientController : Controller
    {
        // Services for Google Calendar and Firebase operations
        private readonly IGoogleCalendarService googleCalendarService;
        private readonly IFirebaseService _firebaseService;

        // Constructor to initialize the services
        public ClientController(IFirebaseService firebaseService, IGoogleCalendarService googleCalendarService)
        {
            _firebaseService = firebaseService;
            this.googleCalendarService = googleCalendarService;
        }

        // Action method for client chat functionality
        public async Task<IActionResult> Chat()
        {
            // Retrieve the user's ID and name from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserId = userId;
            ViewBag.Name = name;

            // Get the list of trainers the client has sessions with
            var trainersFromSessions = await _firebaseService.GetTrainersForClientAsync(userId);

            // Get the list of user IDs from message history
            var messageContactIds = await _firebaseService.GetMessageContactsAsync(userId);

            // Fetch trainer details for these message contacts
            var trainersFromMessages = await _firebaseService.GetTrainersByIdsAsync(messageContactIds);

            // Merge the two lists, removing duplicates
            var allTrainers = trainersFromSessions.Concat(trainersFromMessages)
                .GroupBy(t => t.Id)
                .Select(g => g.First())
                .ToList();

            // Pass the list of contacts to the view
            ViewBag.Contacts = allTrainers;

            // Generate a custom Firebase Auth token for the user
            var firebaseToken = await GenerateFirebaseTokenAsync(userId);
            ViewBag.FirebaseToken = firebaseToken;

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
            string customToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(userId);
            return customToken;
        }

        // Action method to display the calendar
        public async Task<IActionResult> Calendar()
        {
            // Retrieve user details from claims
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserId = userId;
            ViewBag.Name = name;
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var email = User.FindFirstValue(ClaimTypes.Email);

            if (!string.IsNullOrEmpty(accessToken))
            {
                // Get the calendar embed link using the Google Calendar service
                var embedLink = await googleCalendarService.GetCalendarEmbedLinkAsync(accessToken, email);
                ViewBag.CalendarEmbedLink = embedLink;
            }
            else
            {
                ViewBag.CalendarEmbedLink = null;
            }

            return View();
        }

        // Client Dashboard
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Client Dashboard";
            var name = User.FindFirstValue(ClaimTypes.Name); // Retrieve user's name
            ViewBag.Name = name;
            return View();
        }

        // Action method to delete the client's profile
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

                // Optionally, remove user data from the Realtime Database
                await _firebaseService.DeleteUserDataAsync(userId);

                // Sign out the user after deletion
                await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

                TempData["SuccessMessage"] = "Your profile has been deleted successfully.";
                return RedirectToAction("Login", "Account");
            }
            catch (Exception ex)
            {
                // Handle exceptions during profile deletion
                ModelState.AddModelError("", $"Profile deletion failed: {ex.Message}");
                return RedirectToAction("Settings");
            }
        }

        // Action method to save profile changes
        [HttpPost]
        public async Task<IActionResult> SaveProfile(IFormFile photo, string rate)
        {
            TempData["ActiveTab"] = "editProfile"; // Set the active tab to "editProfile"

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);

            try
            {
                string imageUrl = null;

                // If a photo was uploaded, upload it to Firebase Storage
                if (photo != null && photo.Length > 0)
                {
                    imageUrl = await _firebaseService.UploadProfileImageAsync(userId, photo);

                    // Save the image URL to Firebase Realtime Database
                    await _firebaseService.SaveProfileImageUrlAsync(userId, imageUrl);
                }

                // Save the rate to Firebase Realtime Database
                await _firebaseService.SaveRateAsync(userId, rate);

                TempData["SuccessMessage"] = "Profile updated successfully.";
            }
            catch (Exception ex)
            {
                // Handle exceptions during profile update
                ModelState.AddModelError("", $"Profile update failed: {ex.Message}");
            }

            return RedirectToAction("Settings");
        }

        // Action method to display client settings
        public async Task<IActionResult> Settings()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var email = User.FindFirstValue(ClaimTypes.Email);
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve user's name
            ViewBag.Name = Name;
            ViewBag.Email = email;

            if (string.IsNullOrEmpty(userId))
            {
                return RedirectToAction("Login", "Account"); // Redirect to login if the user ID is not found
            }

            // Get the profile image URL from Firebase Realtime Database
            var profileImageUrl = await _firebaseService.GetProfileImageUrlAsync(userId);
            if (profileImageUrl != null)
            {
                ViewBag.ProfileImageUrl = profileImageUrl;
            }
            else
            {
                ViewBag.ProfileImageUrl = Url.Content("~/images/default.jpg");
            }

            // Get the rate from Firebase Realtime Database
            var rate = await _firebaseService.GetRateAsync(userId);
            ViewBag.Rate = rate;

            var model = await _firebaseService.GetAvailabilityAsync(userId);
            model.UserId = userId;
            return View(model); // Pass the availability data to the view
        }

        // Action method to display the list of trainers for booking
        public async Task<IActionResult> BookTrainer()
        {
            var Name = User.FindFirstValue(ClaimTypes.Name); // Retrieve user's name
            ViewBag.Name = Name;
            // Fetch all trainers' data from Firebase (admins only)
            var trainers = await _firebaseService.GetAllTrainersAsync();
            return View(trainers); // Pass the trainer data to the view
        }

        // Action method to book a session with a trainer
        [HttpPost]
        public async Task<IActionResult> BookSession(TrainerAvailabilityViewModel model)
        {
            // Get the client's access token
            var accessToken = await HttpContext.GetTokenAsync("access_token");

            if (string.IsNullOrEmpty(accessToken))
            {
                // Redirect to Google Login if not authenticated
                return RedirectToAction("GoogleLogin", "Account", new { returnUrl = Url.Action("Calendar", "Client") });
            }

            // Get the client's email
            var clientEmail = User.FindFirstValue(ClaimTypes.Email);

            // Get the trainer's details, including email
            var trainer = await _firebaseService.GetTrainerByIdAsync(model.Trainer.Id);
            if (trainer == null)
            {
                return NotFound("Trainer not found.");
            }

            var trainerEmail = trainer.Email;
            if (string.IsNullOrEmpty(trainerEmail))
            {
                return BadRequest("Trainer's email is not available.");
            }

            // Iterate over selected time slots and create calendar events
            foreach (var slot in model.SelectedTimeSlots)
            {
                // Parse date and times
                if (!DateTime.TryParseExact(slot.Date + " " + slot.StartTime, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime startDateTime))
                {
                    continue; // Invalid date/time format
                }

                if (!DateTime.TryParseExact(slot.Date + " " + slot.EndTime, "yyyy-MM-dd HH:mm", null, System.Globalization.DateTimeStyles.None, out DateTime endDateTime))
                {
                    continue; // Invalid date/time format
                }
                var dftStartDateTime = startDateTime;
                var dftEndDateTime = endDateTime;

                var timeZone = TimeZoneInfo.FindSystemTimeZoneById("South Africa Standard Time");
                startDateTime = TimeZoneInfo.ConvertTimeToUtc(startDateTime, timeZone);
                endDateTime = TimeZoneInfo.ConvertTimeToUtc(endDateTime, timeZone);

                // Create calendar event and get the EventId
                var eventId = await CreateCalendarEvent(accessToken, clientEmail, trainerEmail, startDateTime, endDateTime, trainer.Name);

                // Create the BookedSession object
                BookedSession session = new BookedSession
                {
                    TrainerID = trainer.Id,
                    ClientID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Paid = false,
                    TotalAmount = trainer.HourlyRate,
                    StartDateTime = dftStartDateTime.ToString("o"),  // Store as ISO 8601 string
                    EndDateTime = dftEndDateTime.ToString("o"),
                    EventId = eventId  // Store the EventId
                };

                // Save the session to Firebase
                await _firebaseService.PutBookedSession(session, trainer.Id, User.FindFirstValue(ClaimTypes.NameIdentifier), User.FindFirstValue(ClaimTypes.Name), startDateTime);
            }

            // Redirect to the calendar page or a confirmation page
            return RedirectToAction("Calendar");
        }

        // Helper method to create a calendar event
        private async Task<string> CreateCalendarEvent(string accessToken, string clientEmail, string trainerEmail, DateTime startDateTime, DateTime endDateTime, string trainerName)
        {
            var clientName = User.FindFirstValue(ClaimTypes.Name);
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Alleysway Gym",
            });

            var newEvent = new Event()
            {
                Summary = $"Training session with {trainerName}. Booked by: {clientName}",
                Start = new EventDateTime()
                {
                    DateTime = startDateTime,
                    TimeZone = "Africa/Johannesburg",
                },
                End = new EventDateTime()
                {
                    DateTime = endDateTime,
                    TimeZone = "Africa/Johannesburg",
                },
                Attendees = new List<EventAttendee>()
                {
                    new EventAttendee() { Email = trainerEmail }
                },
                Reminders = new Event.RemindersData()
                {
                    UseDefault = true
                }
            };

            var request = calendarService.Events.Insert(newEvent, "primary");
            request.SendUpdates = EventsResource.InsertRequest.SendUpdatesEnum.All;

            var createdEvent = await request.ExecuteAsync();

            // Return the EventId
            return createdEvent.Id;
        }

        // Action method for Google Login
        [HttpGet]
        public IActionResult GoogleLogin(string returnUrl = "/Client/Calendar")
        {
            var properties = new AuthenticationProperties { RedirectUri = returnUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        // Action method to handle Google sign-in response
        [HttpGet("signin-google-client")]
        public async Task<IActionResult> GoogleResponse(string returnUrl = "/Client/Calendar")
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
                return BadRequest("Error while authenticating with Google.");

            return LocalRedirect(returnUrl);  // Redirect to the calendar page after Google sign-in
        }

        // Action method to display a trainer's availability
        public async Task<IActionResult> TrainerAvailability(string id)
        {
            var Name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = Name;
            if (string.IsNullOrEmpty(id))
            {
                return BadRequest("Trainer ID is required.");
            }

            var trainer = await _firebaseService.GetTrainerByIdAsync(id);
            if (trainer == null)
            {
                return NotFound("Trainer not found.");
            }

            // Retrieve general weekly availability and convert to hourly segments
            var rawAvailability = await _firebaseService.GetRawAvailabilityAsync(id);
            var hourlyAvailability = _firebaseService.ConvertToHourlySegments(rawAvailability);

            // Retrieve date-specific availability and convert to hourly segments
            DateTime today = DateTime.Now.Date;
            var dateSpecificAvailability = new Dictionary<string, List<TimeSlot>>();

            for (int i = 0; i < 7; i++)
            {
                var date = today.AddDays(i + 1).ToString("yyyy-MM-dd");
                var slots = await _firebaseService.GetDateSpecificAvailabilityAsync(id, date);

                if (slots.Any())
                {
                    dateSpecificAvailability[date] = new List<TimeSlot>();

                    // Convert each slot in DateSpecificAvailability to hourly segments
                    foreach (var slot in slots)
                    {
                        var startTime = DateTime.Parse($"{date} {slot.StartTime}");
                        var endTime = DateTime.Parse($"{date} {slot.EndTime}");

                        // Create hourly segments
                        while (startTime < endTime)
                        {
                            var nextHour = startTime.AddHours(1);
                            if (nextHour > endTime)
                                nextHour = endTime;

                            dateSpecificAvailability[date].Add(new TimeSlot
                            {
                                StartTime = startTime.ToString("HH:mm"),
                                EndTime = nextHour.ToString("HH:mm"),
                                IsFullDayUnavailable = slot.IsFullDayUnavailable
                            });

                            startTime = nextHour;
                        }
                    }
                }
            }

            // Fetch all booked sessions to filter out unavailable slots
            DateTime currentDateTime = DateTime.Now;
            var bookedSessions = await _firebaseService.GetFutureBookedSessionsForTrainerAsync(id, currentDateTime);

            // Remove booked time slots from general availability and date-specific availability
            foreach (var bookedSession in bookedSessions)
            {
                var startDateTime = DateTime.Parse(bookedSession.StartDateTime);
                var endDateTime = DateTime.Parse(bookedSession.EndDateTime);
                var dayOfWeek = startDateTime.DayOfWeek.ToString();
                var formattedDate = startDateTime.ToString("yyyy-MM-dd");

                // Remove from date-specific availability if it exists
                if (dateSpecificAvailability.ContainsKey(formattedDate))
                {
                    var dateSlots = dateSpecificAvailability[formattedDate];
                    var slotToRemove = dateSlots.FirstOrDefault(ts =>
                        ts.StartTime == startDateTime.ToString("HH:mm") &&
                        ts.EndTime == endDateTime.ToString("HH:mm")
                    );

                    if (slotToRemove != null)
                    {
                        dateSlots.Remove(slotToRemove);
                    }
                }
                else
                {
                    // Otherwise, remove from general availability
                    var dayAvailability = hourlyAvailability.Days.FirstOrDefault(d => d.Day == dayOfWeek);
                    if (dayAvailability != null)
                    {
                        var slotToRemove = dayAvailability.TimeSlots.FirstOrDefault(ts =>
                            ts.StartTime == startDateTime.ToString("HH:mm") &&
                            ts.EndTime == endDateTime.ToString("HH:mm")
                        );
                        if (slotToRemove != null)
                        {
                            dayAvailability.TimeSlots.Remove(slotToRemove);
                        }
                    }
                }
            }

            // Build a ViewModel to pass both weekly and date-specific availability
            var viewModel = new TrainerAvailabilityViewModel
            {
                Trainer = trainer,
                Availability = hourlyAvailability,
                DateSpecificAvailability = dateSpecificAvailability
            };

            return View(viewModel);
        }

        // Action method to view sessions with a specific trainer
        public async Task<IActionResult> TrainerSessions(string trainerId)
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            // Get the sessions between the client and the trainer
            var sessions = await _firebaseService.GetSessionsBetweenTrainerAndClientAsync(trainerId, clientId);

            // Get trainer details
            var trainer = await _firebaseService.GetTrainerByIdAsync(trainerId);

            var viewModel = new TrainerSessionsViewModel
            {
                Trainer = trainer,
                Sessions = sessions // Ensure this matches the type in the ViewModel
            };

            return View(viewModel); // Return the view model
        }

        // Action method to display client's sessions
        public async Task<IActionResult> MySessions()
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            // Fetch all sessions for the client
            var clientSessions = await _firebaseService.GetClientSessionsAsync(clientId);

            return View(clientSessions); // Return the sessions
        }

        // Action method to cancel a session
        [HttpPost]
        public async Task<IActionResult> CancelSession(string sessionId, string trainerId)
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var clientName = User.FindFirstValue(ClaimTypes.Name);

            if (string.IsNullOrEmpty(clientId) || string.IsNullOrEmpty(sessionId) || string.IsNullOrEmpty(trainerId))
            {
                TempData["ErrorMessage"] = "Invalid session data.";
                return RedirectToAction("MySessions");
            }

            try
            {
                // Check if the session exists and belongs to the client
                var session = await _firebaseService.GetSessionAsync(clientId, sessionId);
                if (session == null || session.ClientID != clientId)
                {
                    TempData["ErrorMessage"] = "Session not found or access denied.";
                    return RedirectToAction("MySessions");
                }

                // Enforce cancellation policies
                if (DateTime.Parse(session.StartDateTime) <= DateTime.Now)
                {
                    TempData["ErrorMessage"] = "Cannot cancel past or ongoing sessions.";
                    return RedirectToAction("MySessions");
                }

                // Get the access token to authenticate with Google Calendar API
                var accessToken = await HttpContext.GetTokenAsync("access_token");

                if (!string.IsNullOrEmpty(accessToken) && !string.IsNullOrEmpty(session.EventId))
                {
                    // Delete the calendar event
                    await DeleteCalendarEvent(accessToken, session.EventId);
                }

                // Cancel the session (delete from Firebase) and send message
                await _firebaseService.CancelSessionAsync(clientId, trainerId, sessionId, clientName, session);

                TempData["SuccessMessage"] = "Session canceled successfully.";
            }
            catch (Exception ex)
            {
                // Handle exceptions during session cancellation
                TempData["ErrorMessage"] = $"Error canceling session: {ex.Message}";
            }

            return RedirectToAction("MySessions");
        }

        // Helper method to delete a calendar event
        private async Task DeleteCalendarEvent(string accessToken, string eventId)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Alleysway Gym",
            });

            var request = calendarService.Events.Delete("primary", eventId);
            request.SendUpdates = EventsResource.DeleteRequest.SendUpdatesEnum.All; // Notify all attendees

            await request.ExecuteAsync();
        }

        // Action method to view client portfolio
        public async Task<IActionResult> ClientPortfolio()
        {
            var clients = await _firebaseService.GetAllClientsAsync(); // Fetch all clients
            return View(clients);
        }
    }
}
