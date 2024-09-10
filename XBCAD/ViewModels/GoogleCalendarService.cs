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
                ApplicationName = "Your Application Name",
            });

            // Use the primary calendar for now.
            string calendarId = "primary";

            // Create the embed URL for the user's calendar.
            string calendarEmbedUrl = $"https://calendar.google.com/calendar/embed?src={calendarId}&ctz=America%2FNew_York";
            return calendarEmbedUrl;
        }
    }
}