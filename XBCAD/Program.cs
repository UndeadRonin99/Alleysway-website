using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.Extensions.Logging;
using XBCAD.ViewModels; // Ensure you're referencing your ViewModels

namespace XBCAD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Initialize Firebase Admin SDK with credentials
            var credential = GoogleCredential.FromFile("path/to/alleysway-310a8-firebase-adminsdk-n95a3-ac9e5a55d9.json");
            if (FirebaseApp.DefaultInstance == null)
            {
                FirebaseApp.Create(new AppOptions()
                {
                    Credential = credential
                });
            }

            // Add services to the container
            builder.Services.AddControllersWithViews();
            builder.Services.AddHttpClient();
            builder.Services.AddSingleton<FirebaseService>();
            builder.Services.AddSingleton<GoogleCalendarService>(); // Register GoogleCalendarService

            // Add memory cache to store session data
            builder.Services.AddDistributedMemoryCache();

            // Add session configuration
            builder.Services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(30); // Session timeout
                options.Cookie.HttpOnly = true; // Secure cookies
                options.Cookie.IsEssential = true; // Always send cookies
            });

            // Add Authentication
            builder.Services.AddAuthentication(options =>
            {
                options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
            })
            .AddCookie(options =>
            {
                options.LoginPath = "/Account/Login"; // Customize your login path
                options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Sliding expiration for cookie
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true; // Secure cookie
                options.Cookie.SecurePolicy = CookieSecurePolicy.Always; // Use HTTPS for cookies
                options.Cookie.SameSite = SameSiteMode.None; // Allow cross-site requests for OAuth
            })
           .AddGoogle(googleOptions =>
           {
               googleOptions.ClientId = "890106355046-i4t8lqblco7om7cc9sf3hq9k57f5k62e.apps.googleusercontent.com"; // Replace with your client ID
               googleOptions.ClientSecret = "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"; // Replace with your client secret
               googleOptions.CallbackPath = "/signin-google"; // Ensure this matches the redirect URI in Google Developer Console
               googleOptions.SaveTokens = true; // Save Google tokens
               googleOptions.AccessDeniedPath = "/Account/AccessDenied"; // Handle "Cancel" scenario

               // Add Google Calendar OAuth scope
               googleOptions.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
               googleOptions.Scope.Add("https://www.googleapis.com/auth/calendar");

           
            // Handle authentication failures
            googleOptions.Events.OnRemoteFailure = context =>
                {
                    // Log the error if desired
                    var logger = context.HttpContext.RequestServices.GetRequiredService<ILogger<Program>>();
                    logger.LogWarning("Authentication failed: {Error}", context.Failure?.Message);

                    // Redirect to the login page with an optional error message
                    context.Response.Redirect("/Account/Login?error=authentication_failed");
                    context.HandleResponse(); // Prevent the exception from propagating
                    return Task.CompletedTask;
                };
            });

            var app = builder.Build();

            // Configure the HTTP request pipeline
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Home/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection(); // Ensure HTTPS redirection
            app.UseStaticFiles(); // Serve static files

            app.UseRouting(); // Enable routing

            app.UseSession(); // Enable session support
            app.UseAuthentication(); // Enable authentication
            app.UseAuthorization(); // Enable authorization

            // Configure route mapping
            app.MapControllerRoute(
               name: "default",
               pattern: "{controller=Account}/{action=Home}/{id?}");

            app.Run(); // Start the app
        }
    }
}
