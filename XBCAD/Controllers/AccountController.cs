using Firebase.Database.Query;
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
        // Firebase authentication instance
        private readonly FirebaseAdmin.Auth.FirebaseAuth auth;

        // HTTP client for making requests
        private readonly HttpClient httpClient;

        // Constructor to initialize HTTP client and Firebase authentication
        public AccountController(IHttpClientFactory httpClientFactory)
        {
            this.httpClient = httpClientFactory.CreateClient();

            // Ensure Firebase app is initialized
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions
                {
                    Credential = GoogleCredential.GetApplicationDefault(),
                });
            }
            this.auth = FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance;
        }

        // Render view for account deletion steps
        public IActionResult RequestDeletionSteps()
        {
            return View();
        }

        // Render About Us page
        public IActionResult AboutUs()
        {
            return View();
        }

        // Render Privacy Policy page
        public IActionResult Privacy()
        {
            return View();
        }

        // Render Terms of Service (TOS) page
        public IActionResult tos()
        {
            return View();
        }

        // Render Gallery page
        public IActionResult Gallery()
        {
            return View();
        }

        // Render Contact Us page
        public IActionResult ContactUs()
        {
            return View();
        }

        // Render Home page
        public IActionResult Home()
        {
            return View();
        }

        // Handles redirection after Google Authentication
        [HttpGet("Intermediate")]
        public async Task<IActionResult> IntermediatePage()
        {
            // Authenticate using Google OAuth
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);
            if (!authenticateResult.Succeeded)
            {
                // If authentication fails, redirect to login
                TempData["ErrorMessage"] = "Google authentication was canceled or failed. Please try again.";
                return RedirectToAction("Login", "Account");
            }

            // Retrieve user details from Google authentication result
            var googleUid = authenticateResult.Principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = authenticateResult.Principal.FindFirst(ClaimTypes.Email)?.Value;
            var firstName = authenticateResult.Principal.FindFirst(ClaimTypes.GivenName)?.Value;
            var lastName = authenticateResult.Principal.FindFirst(ClaimTypes.Surname)?.Value;

            // Replace dots with commas in email for Firebase compatibility
            var encodedEmail = email.Replace('.', ',');
            if (string.IsNullOrWhiteSpace(googleUid) || string.IsNullOrWhiteSpace(email))
            {
                return BadRequest("Required Google UID or email is missing.");
            }

            string role = "client"; // Default role assignment
            try
            {
                // Firebase service to manage user data
                var firebaseService = new FirebaseService();
                var userDetails = await firebaseService.CheckUserByEmailAsync(email);

                if (userDetails != null)
                {
                    // Handle existing user data in Firebase
                    var existingUid = userDetails["uid"].ToString();
                    if (!string.Equals(existingUid, googleUid, StringComparison.Ordinal))
                    {
                        // Update Firebase entry with new Google UID
                        var finalData = userDetails;
                        finalData["firstName"] = firstName;
                        finalData["lastName"] = lastName;
                        finalData["email"] = email;
                        finalData.Remove("uid");

                        // Add updated data to new UID
                        await firebaseService.firebase
                            .Child("users")
                            .Child(googleUid)
                            .PutAsync(finalData);

                        // Delete old user entry from Firebase
                        await firebaseService.firebase
                            .Child("users")
                            .Child(existingUid)
                            .DeleteAsync();

                        // Delete old user account in Firebase Auth
                        await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.DeleteUserAsync(existingUid);

                        // Create a new Firebase Auth account for the user
                        var userRecord = await auth.CreateUserAsync(new UserRecordArgs
                        {
                            Uid = googleUid,
                            Email = email,
                            Disabled = false
                        });
                    }
                    else
                    {
                        // Update existing user details
                        var updates = new Dictionary<string, object>
                        {
                            { "firstName", firstName },
                            { "lastName", lastName },
                            { "email", email }
                        };
                        await firebaseService.firebase
                            .Child("users")
                            .Child(googleUid)
                            .PatchAsync(updates);
                    }

                    // Set the user's role if available
                    if (userDetails.ContainsKey("role"))
                    {
                        role = userDetails["role"].ToString();
                    }
                }
                else
                {
                    // Check for pending user setup in Firebase
                    var pendingUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/pending_users/{encodedEmail}.json";
                    var pendingResponse = await httpClient.GetStringAsync(pendingUrl);
                    var userData = JsonSerializer.Deserialize<Dictionary<string, string>>(pendingResponse);

                    if (userData != null && userData.ContainsKey("role"))
                    {
                        // Handle pending user setup
                        role = userData["role"];
                        var rate = userData["ptRate"];

                        try
                        {
                            var finalData = new { firstName, lastName, role, rate, email };
                            var finalJson = JsonSerializer.Serialize(finalData);
                            var finalContent = new StringContent(finalJson, Encoding.UTF8, "application/json");
                            var finalUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                            var response = await httpClient.PutAsync(finalUrl, finalContent);

                            if (!response.IsSuccessStatusCode)
                            {
                                var errorResponse = await response.Content.ReadAsStringAsync();
                                return BadRequest($"Failed to finalize user setup: {errorResponse}");
                            }

                            // Delete pending user entry after processing
                            await httpClient.DeleteAsync(pendingUrl);
                        }
                        catch (Exception ex)
                        {
                            return BadRequest($"Error finalizing pending user setup: {ex.Message}");
                        }
                    }
                    else
                    {
                        // Attempt to fetch and create user if not found
                        UserRecord userRecord;
                        try
                        {
                            // Retrieve existing user details from Firebase
                            userRecord = await auth.GetUserAsync(googleUid);

                            // Fetch user's role from Firebase database
                            var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}/role.json";
                            var roleResponse = await httpClient.GetStringAsync(url);
                            role = JsonSerializer.Deserialize<string>(roleResponse);

                            // Update user information in Firebase
                            var updateUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                            var updateData = new { firstName, lastName, email };
                            var updateJson = JsonSerializer.Serialize(updateData);
                            var updateContent = new StringContent(updateJson, Encoding.UTF8, "application/json");
                            await httpClient.PatchAsync(updateUrl, updateContent);
                        }
                        catch (FirebaseAdmin.Auth.FirebaseAuthException ex)
                        {
                            if (ex.AuthErrorCode == AuthErrorCode.UserNotFound)
                            {
                                // If user is not found, create a new Firebase Auth account
                                role = "client"; // Assign default role
                                userRecord = await auth.CreateUserAsync(new UserRecordArgs
                                {
                                    Uid = googleUid,
                                    Email = email,
                                    Disabled = false
                                });

                                // Initialize new user data in Firebase database
                                var data = new { role = role, firstName, lastName, email };
                                var jsonData = JsonSerializer.Serialize(data);
                                var content = new StringContent(jsonData, Encoding.UTF8, "application/json");
                                var initUrl = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{googleUid}.json";
                                await httpClient.PutAsync(initUrl, content);
                            }
                            else
                            {
                                throw; // Rethrow the exception for unexpected errors
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                // Log the exception for debugging purposes
                Console.WriteLine($"Error checking or processing pending users: {ex.Message}");
            }

            // Redirect user based on their role
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

        public IActionResult Login()
        {
            // Render login page with an empty model
            return View(new LoginViewModel());
        }

        [HttpPost]
        public async Task<IActionResult> VerifyToken(string token)
        {
            try
            {
                // Verify the provided Firebase ID token
                var decodedToken = await FirebaseAdmin.Auth.FirebaseAuth.DefaultInstance.VerifyIdTokenAsync(token);
                var uid = decodedToken.Uid;
                return Ok("User verified with UID: " + uid);
            }
            catch (Exception ex)
            {
                // Return error message if verification fails
                return BadRequest("Token verification failed: " + ex.Message);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model); // Return login view with validation errors
            }

            try
            {
                // Verify Firebase token for login
                var decodedToken = await auth.VerifyIdTokenAsync(model.Token);
                var uid = decodedToken.Uid;

                // Retrieve user details from Firebase database
                var url = $"https://alleysway-310a8-default-rtdb.firebaseio.com/users/{uid}.json";
                var response = await httpClient.GetStringAsync(url);
                var data = JObject.Parse(response);

                var firstName = data["firstName"]?.ToString();
                var lastName = data["lastName"]?.ToString();
                var role = data["role"]?.ToString();

                // Store user details in TempData for the session
                TempData["FirstName"] = firstName;
                TempData["LastName"] = lastName;

                // Create claims for user authentication
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.NameIdentifier, uid),
                    new Claim(ClaimTypes.Name, $"{firstName} {lastName}"),
                    new Claim(ClaimTypes.Email, model.Username),
                    new Claim(ClaimTypes.Role, role)
                };

                var claimsIdentity = new ClaimsIdentity(claims, "Cookies");

                // Sign in the user with claims
                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(claimsIdentity));

                // Redirect to appropriate dashboard based on role
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
            // Configure Google login properties
            var properties = new AuthenticationProperties
            {
                RedirectUri = returnUrl,
                Items = { { "LoginProvider", GoogleDefaults.AuthenticationScheme } }
            };

            // Specify required Google OAuth scopes
            properties.Parameters["scope"] = "openid profile email https://www.googleapis.com/auth/calendar";

            // Trigger Google login challenge
            return Challenge(properties, GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet("signin-google-admin")]
        public async Task<IActionResult> GoogleResponse(string returnUrl = "/Intermediate")
        {
            // Handle Google OAuth response
            var authenticateResult = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

            if (!authenticateResult.Succeeded)
            {
                // Redirect to login on failure
                TempData["ErrorMessage"] = "Google authentication was canceled or failed. Please try again.";
                return RedirectToAction("Login", "Account");
            }

            // Redirect to intermediate page on success
            return LocalRedirect(returnUrl);
        }

        public IActionResult Logout()
        {
            // Redirect user to login after logout
            return RedirectToAction("Login");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            // Redirect user to login with an error message for access denial
            TempData["ErrorMessage"] = "Google authentication was canceled. Please try again.";
            return RedirectToAction("Login");
        }
    }
}
