using Google.Apis.Auth.OAuth2;
using Google.Apis.Calendar.v3;
using Google.Apis.Services;
using System.Threading.Tasks;

namespace XBCAD.ViewModels
{
    public class GoogleCalendarService
    {
        public async Task<string> GetCalendarEmbedLinkAsync(string accessToken)
        {
            var credential = GoogleCredential.FromAccessToken(accessToken);
            var service = new CalendarService(new BaseClientService.Initializer()
            {
                HttpClientInitializer = credential,
                ApplicationName = "Alleyway gym",
            });

            // Use the primary calendar for the user.
            string calendarId = "primary";

            // Set the timezone to Africa/Johannesburg (South Africa timezone)
            string calendarEmbedUrl = $"https://calendar.google.com/calendar/embed?src={calendarId}&ctz=Africa/Johannesburg";
            return calendarEmbedUrl;
        }
    }
}
