using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using System.Threading.Tasks;

namespace XBCAD.ViewModels
{
    public class GoogleCalendarService
    {
        public async Task<string> GetCalendarEmbedLinkAsync(string accessToken, string email)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Alleysway gym",
            });

            // Use the primary calendar for the user.
            string calendarId = encodeEmail(email);

            // Set the timezone to Africa/Johannesburg (South Africa timezone)
            string calendarEmbedUrl = $"https://calendar.google.com/calendar/embed?src={calendarId}&ctz=Africa/Johannesburg";
            return calendarEmbedUrl;
        }

        public string encodeEmail(string email)
        {
            string encodedEmail = email.Replace("@", "%40");
            return encodedEmail;
        }
    }
}
