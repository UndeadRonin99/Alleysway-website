using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
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