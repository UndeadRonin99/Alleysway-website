using FirebaseAdmin;
using FirebaseAdmin.Auth;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies; // For JSON parsing
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using XBCAD.ViewModels;

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

        [HttpGet("Intermediate")]
        public async Task<IActionResult> IntermediatePage()
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
            {
                return BadRequest("Google authentication failed.");
            }

            var googleUid = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = authenticateResult.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = authenticateResult.Principal.FindFirst(ClaimTypes.Surname)?.Value;

            // Replace dot with comma in the email before using it in the Firebase path
            var encodedEmail = email.Replace('.', ',');
            if (string.IsNullOrWhiteSpace(googleUid) || string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Required Google UID or email is missing.");
            }

            string role = "client";
            try
            {
                try
                {
                    // Attempt to find pending user setup
                    var pendingUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/pending_users/{encodedEmail}.json";
                    var pendingResponse = await httpClient.GetStringAsync(pendingUrl);
                    var userData = JsonSerializer.Deserialize<Dictionary<string, string>>(pendingResponse);
                    role = userData["role"];  // Use the role from pending setup
                    var rate = userData["ptRate"];

                    if (userData != null && userData.ContainsKey("role"))
                    {
                        try
                        {
                            // Process pending user
                            var finalData = new { firstName, lastName, role, rate};
                            var finalJson = JsonSerializer.Serialize(finalData);
                            var finalContent = new StringContent(finalJson, Encoding.UTF8, "application/json");
                            var finalUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                            var response = await httpClient.PutAsync(finalUrl, finalContent);

                            if (!response.IsSuccessStatusCode)
                            {
                                var errorResponse = await response.Content.ReadAsStringAsync();
                                return BadRequest($"Failed to finalize user setup: {errorResponse}");
                            }

                            // Optionally, delete the pending entry
                            await httpClient.DeleteAsync(pendingUrl);
                        }
                        catch (Exception ex)
                        {
                            return BadRequest($"Error finalizing pending user setup: {ex.Message}");
                        }
                    }
                    else
                    {
                        return BadRequest("Malformed user");
                    }
                }
                catch
                {
                    UserRecord userRecord;
                    try
                    {
                        userRecord = await auth.GetUserAsync(googleUid);
                        // Fetch role from Firebase RTDB
                        var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}/role.json";
                        var roleResponse = await httpClient.GetStringAsync(url);
                        role = JsonSerializer.Deserialize<string>(roleResponse);

                        // Update name and other details if needed
                        var updateUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                        var updateData = new { firstName, lastName };
                        var updateJson = JsonSerializer.Serialize(updateData);
                        var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
                        await httpClient.PatchAsync(updateUrl, updateContent);
                    }
                    catch (FirebaseAuthException ex)
                    {
                        if (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                        {
                            // Default to client if not specified
                            role = "client";
                            userRecord = await auth.CreateUserAsync(new UserRecordArgs
                            {
                                Uid = googleUid,
                                Email = email,
                                Disabled = false
                            });

                            // Initialize default data in Firebase
                            var data = new { role = role, firstName, lastName };
                            var jsonData = JsonSerializer.Serialize(data);
                            var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                            var initUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                            await httpClient.PutAsync(initUrl, content);
                        }
                        else
                        {
                            throw;
                        }
                    }

                }
            }
            catch (Exception ex)
            {
                // Log this exception; consider creating a new user if none exists
                Console.WriteLine($"Error checking or processing pending users: {ex.Message}");
            }

            // Redirect based on role
            switch (role)
            {
                case "admin":
                    return RedirectToAction("Dashboard", "Admin");
                case "client":
                    return RedirectToAction("Dashboard", "Client");
                default:
                    return BadRequest("User role is not defined properly.");
            }
        }




        [HttpGet("Register")]
        public IActionResult Register()
        {
            return View();
        }
        //test1

        //FOR THE LOVE OF ALL THINGS HOLY PLEASE DO NOT FUCKING TOUCH THIS REGISTER METHOD EVER
        //IT WORKS NOW AND I REALLY DON'T WANT TO HAVE TO TOUCH IT AGAIN
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
                    var data = new
                    {
                        firstName = model.FirstName,
                        lastName = model.LastName,
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
                        return RedirectToAction("Login");
                    }
                    else
                    {
                        throw new Exception("Failed to save user data to database.");
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
                return View(model);
            }

            try
            {
                // Verify the Firebase ID token
                var decodedToken = await auth.VerifyIdTokenAsync(model.Token);
                var uid = decodedToken.Uid;

                // Fetch user data from Firebase RTDB
                var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{uid}.json";
                var response = await httpClient.GetStringAsync(url);
                var data = JObject.Parse(response);

                var firstName = data["firstName"]?.ToString();
                var lastName = data["lastName"]?.ToString();
                var role = data["role"]?.ToString();

                // Store the name and surname in TempData to pass to the next view
                TempData["FirstName"] = firstName;
                TempData["LastName"] = lastName;
                // Create claims for the user
                var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, uid),
                        new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                        new Claim(ClaimTypes.Email, model.Username),  // Add Username as Email claim
                        new Claim(ClaimTypes.Role, role)
                            // Add other claims as needed
                    };

                var claimsIdentity = new ClaimsIdentity(claims, "Cookies");

                // Sign in the user with the created claims
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));


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

        [HttpGet]
        public IActionResult GoogleLogin(string returnUrl = "/Intermediate")
        {
            var properties = new AuthenticationProperties { RedirectUri = returnUrl };
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("signin-google-admin")]
        public async Task<IActionResult> GoogleResponse(string returnUrl = "/Intermediate")
        {
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
                return BadRequest("Error while authenticating with Google.");


            // Perform local sign-in as necessary or redirect
            return LocalRedirect(returnUrl);
        }




        public IActionResult Logout()
        {
            return RedirectToAction("Login");
        }
    }
}