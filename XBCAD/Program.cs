using FirebaseAdmin;
using Google.Apis.Auth.OAuth2;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.Google;
using XBCAD.ViewModels; // Make sure you're referencing your ViewModels

namespace XBCAD
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Initialize Firebase Admin SDK with credentials
            var credential = GoogleCredential.FromFile("path/to/alleysway-310a8-firebase-adminsdk-n95a3-af1016f422.json");
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
            })
            .AddGoogle(googleOptions =>
            {
                googleOptions.ClientId = "1073270743918-s6b88etdi7v3v3dbisl1cs7fljgekgn8.apps.googleusercontent.com"; // Replace with your client ID
                googleOptions.ClientSecret = "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"; // Replace with your client secret
                googleOptions.CallbackPath = "/signin-google"; // Ensure this matches the redirect URI in Google Developer Console
                googleOptions.SaveTokens = true; // Save Google tokens

                // Add Google Calendar OAuth scope
                googleOptions.Scope.Add("https://www.googleapis.com/auth/calendar.readonly");
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
               pattern: "{controller=Account}/{action=Login}/{id?}");

            app.Run(); // Start the app
        }
    }
}
