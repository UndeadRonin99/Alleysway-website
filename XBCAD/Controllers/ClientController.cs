using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using System.Threading.Tasks;
using XBCAD.ViewModels;

namespace XBCAD.Controllers
{
    public class ClientController : Controller
    {
        private readonly FirebaseService _firebaseService;

        public ClientController(FirebaseService firebaseService)
        {
            _firebaseService = firebaseService;
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
            // Fetch all trainers' data from Firebase (admins only)
            var trainers = await _firebaseService.GetAllTrainersAsync();
            return View(trainers); // Pass the trainer data to the view
        }

        public async Task<IActionResult> TrainerAvailability(string id)
        {
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