using System;
using System.Threading.Tasks;

namespace XBCAD.Tests.Mocks
{
    public class MockGoogleCalendarService : IGoogleCalendarService
    {
        public Task<string> GetCalendarEmbedLinkAsync(string accessToken, string email)
        {
            // Return a mock embed link
            var embedLink = $"https://calendar.google.com/calendar/embed?src={Uri.EscapeDataString(email)}&ctz=Africa/Johannesburg";
            return Task.FromResult(embedLink);
        }

        // Implement other methods from IGoogleCalendarService if necessary
    }
}
