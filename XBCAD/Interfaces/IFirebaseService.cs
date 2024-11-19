using XBCAD.ViewModels;

public interface IFirebaseService
{
    Task<Dictionary<string, object>> CheckUserByEmailAsync(string email);
    Task<ClientPaymentSummaryViewModel> GetClientPaymentSummaryAsync(string clientId);
    Task UpdateSessionPaymentStatusAsync(string userId, string sessionId, bool isPaid);
    Task<List<BookedSession>> GetSessionsBetweenTrainerAndClientAsync(string trainerId, string clientId);
    Task<ClientViewModel> GetClientByIdAsync(string clientId);
    Task<List<Trainer>> GetTrainersForClientAsync(string clientId);
    Task<List<ClientViewModel>> GetClientsForTrainerAsync(string trainerId);
    Task PutBookedSession(BookedSession session, string trainerID, string userID, string userName, DateTime dateTime);
    Task<AvailabilityViewModel> GetRawAvailabilityAsync(string userId);
    AvailabilityViewModel ConvertToHourlySegments(AvailabilityViewModel rawAvailability);
    Task<Trainer> GetTrainerByIdAsync(string userId);
    Task<List<Trainer>> GetAllTrainersAsync();
    Task DeleteUserDataAsync(string userId);
    Task<string> GetRateAsync(string userId);
    Task SaveRateAsync(string userId, string rate);
    Task<string> UploadProfileImageAsync(string userId, IFormFile photo);
    Task SaveProfileImageUrlAsync(string userId, string imageUrl);
    Task<string> GetProfileImageUrlAsync(string userId);
    Task<AvailabilityViewModel> GetAvailabilityAsync(string userId);
    Task SaveTimeSlotAsync(string day, string startTime, string endTime, string userId);
    Task RemoveTimeSlotAsync(string day, string startTime, string endTime, string userId);
    Task<List<BookedSession>> GetFutureBookedSessionsForTrainerAsync(string trainerId, DateTime currentDateTime);
    Task<List<ClientSessionViewModel>> GetClientSessionsAsync(string clientId);
    Task CancelSessionAsync(string clientId, string trainerId, string sessionId, string clientName, BookedSession session);
    Task<string> SaveDateSpecificUnavailabilityAsync(string userId, string date, string startTime, string endTime, bool isFullDayUnavailable);
    Task<string> SaveDateSpecificAvailabilityAsync(string userId, string date, string startTime, string endTime);
    Task<Dictionary<string, List<DateSpecificTimeSlot>>> GetAllDateSpecificAvailabilityAsync(string userId);
    Task<List<DayAvailability>> GetAllAvailabilityAsync(string userId);
    Task<List<TimeSlot>> GetDateSpecificAvailabilityAsync(string userId, string date);
    Task<bool> RemoveDateSpecificTimeSlotAsync(string userId, string date, string slotId);
    Task<BookedSession> GetSessionAsync(string userId, string sessionId);
    Task<List<string>> GetMessageContactsAsync(string userId);
    Task<List<Trainer>> GetTrainersByIdsAsync(List<string> trainerIds);
    Task<List<ClientViewModel>> GetClientsByIdsAsync(List<string> clientIds);
    Task<List<ClientSessionsViewModel>> GetAllSessionsForTrainerAsync(string trainerId);
    Task<List<ClientViewModel>> GetAllClientsAsync();
}

