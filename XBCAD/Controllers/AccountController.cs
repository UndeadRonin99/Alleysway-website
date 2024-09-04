using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Mvc;
using XBCAD.Models;
using XBCAD.ViewModels;
using System;
using System.Threading.Tasks;
using System.Net.Http;
using System.Text;
using Newtonsoft.Json.Linq;
using System.Text.Json; // For JSON parsing

namespace XBCAD.Controllers
{
    public class AccountController : Controller
    {
        private readonly FirebaseAuth auth;
        private readonly HttpClient httpClient;

        public AccountController(IHttpClientFactory httpClientFactory)
        {
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

        [HttpGet("Register")]
        public IActionResult Register()
        {
            return View();
        }

        [HttpPost("Register")]
        public async Task<IActionResult> Register(RegisterViewModel model)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    

                    var userRecord = await this.auth.CreateUserAsync(new UserRecordArgs
                    {
                        Email = model.Username,
                        Password = model.Password,
                        DisplayName = $"{model.FirstName} {model.LastName}",
                        Disabled = false,
                    });

                    // Prepare data to be saved in RTDB
                    var data = new { role = "admin" };
                    var json = JsonSerializer.Serialize(data);
                    var content = new StringContent(json, Encoding.UTF8, "application/json");

                    // Construct the URL to Firebase RTDB
                    var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{userRecord.Uid}.json";

                    // Send data to Firebase RTDB
                    var response = await httpClient.PutAsync(url, content);

                    if (response.IsSuccessStatusCode)
                    {
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        throw new Exception("Failed to save user role to database.");
                    }
                }
                catch (Exception ex)
                {
                    ModelState.AddModelError("", $"Registration failed: {ex.Message}");
                    return View(model);
                }
            }
            return View(model);
        }

        public IActionResult Login()
        {
            return View(new LoginViewModel());  // Ensure a model instance is passed, even if empty
        }

        [HttpPost]
        public async Task<IActionResult> VerifyToken(string token)
        {
            try
            {
                var decodedToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
                var uid = decodedToken.Uid;
                // Proceed with the user's UID to grant access or fetch user-specific data
                return Ok("User verified with UID: " + uid);
            }
            catch (Exception ex)
            {
                return BadRequest("Token verification failed: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                // Perhaps add more user-friendly error messages or specific logging here
                return View(model);
            }

            try
            {
                // Verify the Firebase ID token
                var decodedToken = await auth.VerifyIdTokenAsync(model.Token);
                var uid = decodedToken.Uid;

                // Fetch user role from Firebase RTDB
                var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{uid}.json";
                var response = await httpClient.GetStringAsync(url);
                var data = JObject.Parse(response);
                var role = data["role"]?.ToString();

                // Redirect based on role
                return role switch
                {
                    "admin" => RedirectToAction("Dashboard", "Admin"),
                    "client" => RedirectToAction("Dashboard", "Client"),
                    _ => throw new Exception("Role not found or unauthorized.")
                };
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", $"Login failed: {ex.Message}");
                return View(model);
            }
        }


        public IActionResult Logout()
        {
            return RedirectToAction("Login");
        }
    }
}
