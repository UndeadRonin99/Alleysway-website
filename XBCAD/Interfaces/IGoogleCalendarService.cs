public interface IGoogleCalendarService
{
    Task<string> GetCalendarEmbedLinkAsync(string accessToken, string email);
}

