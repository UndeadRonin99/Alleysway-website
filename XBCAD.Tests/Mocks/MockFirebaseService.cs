using Microsoft.AspNetCore.Http;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using XBCAD.ViewModels;

namespace XBCAD.Tests.Mocks
{
    public class MockFirebaseService : IFirebaseService
    {
        private readonly Dictionary<string, List<BookedSession>> _trainerSessions;
        private readonly Dictionary<string, List<BookedSession>> _clientSessions;
        private readonly Dictionary<string, BookedSession> _sessionsByKey;
        private readonly Dictionary<string, ClientViewModel> _clients;
        private readonly Dictionary<string, Trainer> _trainers;
        private readonly Dictionary<string, string> _profileImages;
        private readonly Dictionary<string, string> _rates;
        private readonly Dictionary<string, AvailabilityViewModel> _availability;
        private readonly Dictionary<string, Dictionary<string, List<DateSpecificTimeSlot>>> _dateSpecificAvailability;
        private readonly Dictionary<string, List<Message>> _userMessages;

        public MockFirebaseService()
        {
            _trainerSessions = new Dictionary<string, List<BookedSession>>();
            _clientSessions = new Dictionary<string, List<BookedSession>>();
            _sessionsByKey = new Dictionary<string, BookedSession>();
            _clients = new Dictionary<string, ClientViewModel>();
            _trainers = new Dictionary<string, Trainer>();
            _profileImages = new Dictionary<string, string>();
            _rates = new Dictionary<string, string>();
            _availability = new Dictionary<string, AvailabilityViewModel>();
            _dateSpecificAvailability = new Dictionary<string, Dictionary<string, List<DateSpecificTimeSlot>>>();
            _userMessages = new Dictionary<string, List<Message>>();
        }

        // Add a trainer to the mock service
        public void AddTrainer(Trainer trainer)
        {
            _trainers[trainer.Id] = trainer;
        }

        // Add a client to the mock service
        public void AddClient(ClientViewModel client)
        {
            _clients[client.Id] = client;
        }

        // Retrieve a saved session for a trainer on a specific date
        public BookedSession GetSavedSession(string trainerId, string date)
        {
            if (_trainerSessions.ContainsKey(trainerId))
            {
                var sessions = _trainerSessions[trainerId];
                var session = sessions.FirstOrDefault(s => DateTime.Parse(s.StartDateTime).ToString("yyyy-MM-dd") == date);
                return session;
            }
            return null;
        }

        public Task PutBookedSession(BookedSession session, string trainerID, string userID, string userName, DateTime dateTime)
        {
            session.SessionKey = Guid.NewGuid().ToString();

            if (!_trainerSessions.ContainsKey(trainerID))
            {
                _trainerSessions[trainerID] = new List<BookedSession>();
            }

            _trainerSessions[trainerID].Add(session);

            if (!_clientSessions.ContainsKey(userID))
            {
                _clientSessions[userID] = new List<BookedSession>();
            }

            _clientSessions[userID].Add(session);

            _sessionsByKey[session.SessionKey] = session;

            // Add a message to user messages (simplified for mock)
            if (!_userMessages.ContainsKey(trainerID))
            {
                _userMessages[trainerID] = new List<Message>();
            }

            _userMessages[trainerID].Add(new Message
            {
                SenderId = userID,
                ReceiverId = trainerID,
                SenderName = userName,
                Text = $"I've booked a session with you on {dateTime:dd/MM/yyyy} at {dateTime:HH:mm}",
                Timestamp = DateTime.Now
            });

            return Task.CompletedTask;
        }

        public Task<List<BookedSession>> GetFutureBookedSessionsForTrainerAsync(string trainerId, DateTime currentDateTime)
        {
            if (_trainerSessions.ContainsKey(trainerId))
            {
                var futureSessions = _trainerSessions[trainerId]
                    .Where(session => DateTime.Parse(session.StartDateTime) >= currentDateTime)
                    .ToList();
                return Task.FromResult(futureSessions);
            }
            return Task.FromResult(new List<BookedSession>());
        }

        public Task<List<ClientViewModel>> GetClientsForTrainerAsync(string trainerId)
        {
            var clients = new List<ClientViewModel>();

            if (_trainerSessions.ContainsKey(trainerId))
            {
                var sessions = _trainerSessions[trainerId];

                // Get unique client IDs from sessions
                var clientIds = sessions.Select(s => s.ClientID).Distinct();

                foreach (var clientId in clientIds)
                {
                    if (_clients.TryGetValue(clientId, out var client))
                    {
                        clients.Add(client);
                    }
                }
            }

            return Task.FromResult(clients);
        }

        public Task<Dictionary<string, object>> CheckUserByEmailAsync(string email)
        {
            return Task.FromResult(new Dictionary<string, object>());
        }

        public Task<ClientPaymentSummaryViewModel> GetClientPaymentSummaryAsync(string clientId)
        {
            return Task.FromResult(new ClientPaymentSummaryViewModel());
        }

        public Task UpdateSessionPaymentStatusAsync(string userId, string sessionId, bool isPaid)
        {
            if (_sessionsByKey.TryGetValue(sessionId, out var session))
            {
                session.Paid = isPaid;
            }
            return Task.CompletedTask;
        }

        public Task<List<BookedSession>> GetSessionsBetweenTrainerAndClientAsync(string trainerId, string clientId)
        {
            var sessions = new List<BookedSession>();

            if (_trainerSessions.ContainsKey(trainerId))
            {
                sessions = _trainerSessions[trainerId]
                    .Where(s => s.ClientID == clientId)
                    .ToList();
            }

            return Task.FromResult(sessions);
        }

        public Task<ClientViewModel> GetClientByIdAsync(string clientId)
        {
            if (_clients.TryGetValue(clientId, out var client))
                return Task.FromResult(client);

            return Task.FromResult<ClientViewModel>(null);
        }

        public Task<List<Trainer>> GetTrainersForClientAsync(string clientId)
        {
            var trainers = new List<Trainer>();

            if (_clientSessions.ContainsKey(clientId))
            {
                var sessions = _clientSessions[clientId];

                // Get unique trainer IDs from sessions
                var trainerIds = sessions.Select(s => s.TrainerID).Distinct();

                foreach (var trainerId in trainerIds)
                {
                    if (_trainers.TryGetValue(trainerId, out var trainer))
                    {
                        trainers.Add(trainer);
                    }
                }
            }

            return Task.FromResult(trainers);
        }

        public Task<Trainer> GetTrainerByIdAsync(string userId)
        {
            if (_trainers.TryGetValue(userId, out var trainer))
                return Task.FromResult(trainer);

            return Task.FromResult<Trainer>(null);
        }

        public Task<List<Trainer>> GetAllTrainersAsync()
        {
            return Task.FromResult(_trainers.Values.ToList());
        }

        public Task CancelSessionAsync(string clientId, string trainerId, string sessionId, string clientName, BookedSession session)
        {
            throw new NotImplementedException();
        }

        public AvailabilityViewModel ConvertToHourlySegments(AvailabilityViewModel rawAvailability)
        {
            throw new NotImplementedException();
        }

        public Task DeleteUserDataAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<List<DayAvailability>> GetAllAvailabilityAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<List<ClientViewModel>> GetAllClientsAsync()
        {
            throw new NotImplementedException();
        }

        public Task<Dictionary<string, List<DateSpecificTimeSlot>>> GetAllDateSpecificAvailabilityAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<List<ClientSessionsViewModel>> GetAllSessionsForTrainerAsync(string trainerId)
        {
            throw new NotImplementedException();
        }

        public Task<AvailabilityViewModel> GetAvailabilityAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<List<ClientViewModel>> GetClientsByIdsAsync(List<string> clientIds)
        {
            throw new NotImplementedException();
        }

        public Task<List<ClientSessionViewModel>> GetClientSessionsAsync(string clientId)
        {
            throw new NotImplementedException();
        }

        public Task<List<TimeSlot>> GetDateSpecificAvailabilityAsync(string userId, string date)
        {
            throw new NotImplementedException();
        }

        public Task<List<string>> GetMessageContactsAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetProfileImageUrlAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<string> GetRateAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<AvailabilityViewModel> GetRawAvailabilityAsync(string userId)
        {
            throw new NotImplementedException();
        }

        public Task<BookedSession> GetSessionAsync(string userId, string sessionId)
        {
            throw new NotImplementedException();
        }

        public Task<List<Trainer>> GetTrainersByIdsAsync(List<string> trainerIds)
        {
            throw new NotImplementedException();
        }

        public Task<bool> RemoveDateSpecificTimeSlotAsync(string userId, string date, string slotId)
        {
            throw new NotImplementedException();
        }

        public Task RemoveTimeSlotAsync(string day, string startTime, string endTime, string userId)
        {
            throw new NotImplementedException();
        }

        public Task<string> SaveDateSpecificAvailabilityAsync(string userId, string date, string startTime, string endTime)
        {
            throw new NotImplementedException();
        }

        public Task<string> SaveDateSpecificUnavailabilityAsync(string userId, string date, string startTime, string endTime, bool isFullDayUnavailable)
        {
            throw new NotImplementedException();
        }

        public Task SaveProfileImageUrlAsync(string userId, string imageUrl)
        {
            throw new NotImplementedException();
        }

        public Task SaveRateAsync(string userId, string rate)
        {
            throw new NotImplementedException();
        }

        public Task SaveTimeSlotAsync(string day, string startTime, string endTime, string userId)
        {
            throw new NotImplementedException();
        }

        public Task<string> UploadProfileImageAsync(string userId, IFormFile photo)
        {
            throw new NotImplementedException();
        }
    }

    // Mock Message class (assuming you have one)
    public class Message
    {
        public string SenderId { get; set; }
        public string ReceiverId { get; set; }
        public string SenderName { get; set; }
        public string Text { get; set; }
        public DateTime Timestamp { get; set; }
    }
}
