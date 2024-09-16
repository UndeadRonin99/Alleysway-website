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
using System.Text;
using System.Text.Json;
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

        // Client Profile
        public IActionResult Profile()
        {
            ViewData["Title"] = "Profile";
            return View();
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

                // Create calendar event
                await CreateCalendarEvent(accessToken, clientEmail, trainerEmail, startDateTime, endDateTime, trainer.Name);
            }

            // Optional: Save booking details to Firebase or database

            // Redirect to the calendar page or a confirmation page
            return RedirectToAction("Calendar");
        }

        // Helper method to create a calendar event
        private async Task CreateCalendarEvent(string accessToken, string clientEmail, string trainerEmail, DateTime startDateTime, DateTime endDateTime, string trainerName)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);

            var calendarService = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Alleysway Gym",
            });

            var newEvent = new Event()
            {
                Summary = $"Training session with {trainerName}",
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

            await request.ExecuteAsync();
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
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
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

            // Step 1: Fetch raw availability
            var rawAvailability = await _firebaseService.GetRawAvailabilityAsync(id);

            // Step 2: Convert to hourly segments
            var hourlyAvailability = _firebaseService.ConvertToHourlySegments(rawAvailability);

            var viewModel = new TrainerAvailabilityViewModel
            {
                Trainer = trainer,
                Availability = hourlyAvailability // Pass the converted availability
            };

            return View(viewModel);
        }

    }

}