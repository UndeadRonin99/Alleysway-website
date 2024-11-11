﻿using Firebase.Auth;
using FirebaseAdmin;
using FirebaseAdmin.Auth;
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
    public class ClientController : Controller
    {
        private readonly GoogleCalendarService googleCalendarService;
        private readonly FirebaseService _firebaseService;

        public ClientController(FirebaseService firebaseService, GoogleCalendarService calendarService)
        {
            _firebaseService = firebaseService;
            googleCalendarService = calendarService;
        }

        public async Task<IActionResult> Chat()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.UserId = userId;
            ViewBag.Name = name;

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
            string customToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.CreateCustomTokenAsync(userId);
            return customToken;
        }



        public async Task<IActionResult> Calendar()
        {
            var accessToken = await HttpContext.GetTokenAsync("access_token");
            var email = User.FindFirstValue(ClaimTypes.Email);

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

        // Client Dashboard
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Client Dashboard";
            var name = User.FindFirstValue(ClaimTypes.Name); // Retrieve Name
            ViewBag.Name = name;
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
                await _firebaseService.DeleteUserDataAsync(userId);

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
            var profileImageUrl = await _firebaseService.GetProfileImageUrlAsync(userId);
            if(profileImageUrl != null)
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

        public async Task<IActionResult> BookTrainer()
        {
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;
            // Fetch all trainers' data from Firebase (admins only)
            var trainers = await _firebaseService.GetAllTrainersAsync();
            return View(trainers); // Pass the trainer data to the view
        }


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

                // Create calendar event and get the EventId
                var eventId = await CreateCalendarEvent(accessToken, clientEmail, trainerEmail, startDateTime, endDateTime, trainer.Name);

                // Create the BookedSession object
                BookedSession session = new BookedSession
                {
                    TrainerID = trainer.Id,
                    ClientID = User.FindFirstValue(ClaimTypes.NameIdentifier),
                    Paid = false,
                    TotalAmount = trainer.HourlyRate,
                    StartDateTime = startDateTime.ToString("o"),  // Store as ISO 8601 string
                    EndDateTime = endDateTime.ToString("o"),
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

        [HttpGet]
        public IActionResult GoogleLogin(string returnUrl = "/Client/Calendar")
        {
            var properties = new AuthenticationProperties { RedirectUri = returnUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("signin-google-client")]
        public async Task<IActionResult> GoogleResponse(string returnUrl = "/Client/Calendar")
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
                return BadRequest("Error while authenticating with Google.");

            return LocalRedirect(returnUrl);  // Redirect to the calendar page after Google sign-in
        }


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

            var rawAvailability = await _firebaseService.GetRawAvailabilityAsync(id);
            var hourlyAvailability = _firebaseService.ConvertToHourlySegments(rawAvailability);

            // For testing, set the current date to a future date
            DateTime currentDateTime = DateTime.Now;

            // Simulate the day after the session date
            // For example, to simulate 1 day in the future:
            //currentDateTime = currentDateTime.AddDays(4);

            var bookedSessions = await _firebaseService.GetFutureBookedSessionsForTrainerAsync(id, currentDateTime);


            // Step 4: Remove booked time slots from availability
            foreach (var bookedSession in bookedSessions)
            {
                var startDateTime = DateTime.Parse(bookedSession.StartDateTime);
                var dayOfWeek = startDateTime.DayOfWeek.ToString();

                var dayAvailability = hourlyAvailability.Days.FirstOrDefault(d => d.Day == dayOfWeek);
                if (dayAvailability != null)
                {
                    var slotToRemove = dayAvailability.TimeSlots.FirstOrDefault(ts =>
                        ts.StartTime == startDateTime.ToString("HH:mm") &&
                        ts.EndTime == DateTime.Parse(bookedSession.EndDateTime).ToString("HH:mm")
                    );
                    if (slotToRemove != null)
                    {
                        dayAvailability.TimeSlots.Remove(slotToRemove);
                    }
                }
            }

            var viewModel = new TrainerAvailabilityViewModel
            {
                Trainer = trainer,
                Availability = hourlyAvailability
            };

            return View(viewModel);
        }
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

            return View(viewModel); // Ensure you're returning the view model, not the sessions list
        }



        public async Task<IActionResult> MySessions()
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var name = User.FindFirstValue(ClaimTypes.Name);
            ViewBag.Name = name;

            // Fetch all sessions for the client
            var clientSessions = await _firebaseService.GetClientSessionsAsync(clientId);

            return View(clientSessions); // Ensure you're returning the correct view/model
        }

        [HttpPost]
        public async Task<IActionResult> CancelSession(string sessionId, string trainerId)
        {
            var clientId = User.FindFirstValue(ClaimTypes.NameIdentifier);

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

                // Cancel the session (delete from Firebase)
                await _firebaseService.CancelSessionAsync(clientId, trainerId, sessionId);

                TempData["SuccessMessage"] = "Session canceled successfully.";
            }
            catch (Exception ex)
            {
                TempData["ErrorMessage"] = $"Error canceling session: {ex.Message}";
            }

            return RedirectToAction("MySessions");
        }

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

    }


}