﻿using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using XBCAD.ViewModels;


public class FirebaseService
{
    public readonly FirebaseClient firebase;
    private readonly FirebaseStorage storage;


    public FirebaseService()
    {
        // Ensure this URL is correct and points to your Firebase Realtime Database
        firebase = new FirebaseClient("https://alleysway-310a8-default-rtdb.firebaseio.com/");

        // Initialize Firebase Storage 
        storage = new FirebaseStorage("alleysway-310a8.appspot.com");
    }

    public async Task<Dictionary<string, object>> CheckUserByEmailAsync(string email)
    {
        try
        {
            // Query the 'users' node for the given email
            var snapshot = await firebase
                .Child("users")
                .OrderBy("email")
                .EqualTo(email)
                .OnceAsync<dynamic>();

            // Check if the snapshot has any results
            if (snapshot != null && snapshot.Count > 0)
            {
                var user = snapshot.FirstOrDefault();
                if (user != null)
                {
                    var userId = user.Key;

                    // Use JsonConvert to deserialize the user data
                    string jsonData = JsonConvert.SerializeObject(user.Object);
                    var userData = JsonConvert.DeserializeObject<Dictionary<string, object>>(jsonData);

                    if (userData != null)
                    {
                        userData.Add("uid", userId); // Include UID in the returned data
                        return new Dictionary<string, object>(userData);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error checking user by email: {ex.Message}");
        }

        // Return null if no user is found or an error occurred
        return null;
    }



    public async Task<ClientPaymentSummaryViewModel> GetClientPaymentSummaryAsync(string clientId)
    {
        var payments = new List<TrainerPayment>();

        var sessions = await firebase
            .Child("users")
            .Child(clientId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        var paidSessions = sessions
            .Where(s => s.Object.Paid)
            .GroupBy(s => s.Object.TrainerID);

        foreach (var group in paidSessions)
        {
            var trainerId = group.Key;
            var totalAmountPaid = group.Sum(s => s.Object.TotalAmount);

            // Get trainer name
            var trainerData = await firebase
                .Child("users")
                .Child(trainerId)
                .OnceSingleAsync<dynamic>();

            string trainerName = $"{trainerData.firstName} {trainerData.lastName}";

            payments.Add(new TrainerPayment
            {
                TrainerId = trainerId,
                TrainerName = trainerName,
                TotalAmountPaid = totalAmountPaid
            });
        }

        return new ClientPaymentSummaryViewModel
        {
            Payments = payments
        };
    }


    public async Task UpdateSessionPaymentStatusAsync(string userId, string sessionId, bool isPaid)
    {
        await firebase
            .Child("users")
            .Child(userId)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .Child("Paid")
            .PutAsync(isPaid);
    }
    public async Task<List<BookedSession>> GetSessionsBetweenTrainerAndClientAsync(string trainerId, string clientId)
    {
        var sessions = new List<BookedSession>();

        var sessionNodes = await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        foreach (var sessionNode in sessionNodes)
        {
            var session = sessionNode.Object;
            if (session.ClientID == clientId)
            {
                session.SessionKey = sessionNode.Key; // Store the key
                sessions.Add(session);
            }
        }

        return sessions;
    }

    public async Task<ClientViewModel> GetClientByIdAsync(string clientId)
    {
        var clientData = await firebase
            .Child("users")
            .Child(clientId)
            .OnceSingleAsync<dynamic>();

        if (clientData != null)
        {
            string profileImageUrl = clientData.profileImageUrl ?? "/images/default.jpg";
            return new ClientViewModel
            {
                Id = clientId,
                Name = $"{clientData.firstName} {clientData.lastName}",
                ProfileImageUrl = profileImageUrl
            };
        }

        return null;
    }

    public async Task<List<ClientViewModel>> GetClientsForTrainerAsync(string trainerId)
    {
        var clients = new List<ClientViewModel>();

        // Get the sessions for the trainer
        var sessions = await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        // Extract unique client IDs
        var clientIds = sessions.Select(s => s.Object.ClientID).Distinct();

        foreach (var clientId in clientIds)
        {
            // Get client details
            var clientData = await firebase
                .Child("users")
                .Child(clientId)
                .OnceSingleAsync<dynamic>();

            if (clientData != null)
            {
                string profileImageUrl = clientData.profileImageUrl ?? "/images/default.jpg";
                clients.Add(new ClientViewModel
                {
                    Id = clientId,
                    Name = $"{clientData.firstName} {clientData.lastName}",
                    ProfileImageUrl = profileImageUrl
                });
            }
        }

        return clients;
    }

    public async Task PutBookedSession(BookedSession session, string trainerID, string userID, string userName, DateTime dateTime)
    {
        await firebase
            .Child("users")
            .Child(trainerID)
            .Child("sessions")
            .Child("SessionID")
            .PostAsync(session);

        await firebase
            .Child("users")
            .Child(userID)
            .Child("sessions")
            .Child("SessionID")
            .PostAsync(session);

        Message clientMessage = new Message
        {
            senderId = userID,
            receiverId = trainerID,
            senderName = userName,
            text = $"I've booked a session with you on {dateTime.Date.ToString("dd/MM/yyyy")} at {dateTime.TimeOfDay}",
            timestamp = Timestamp.GetCurrentTimestamp()
        };

        await firebase
                .Child("user_messages")
                .Child(userID)
                .Child(trainerID)
                .Child("messages")
                .PostAsync(clientMessage);

        await firebase
            .Child("user_messages")
            .Child(trainerID)
            .Child(userID)
            .Child("messages")
            .PostAsync(clientMessage);
    }

    public async Task<AvailabilityViewModel> GetRawAvailabilityAsync(string userId)
    {
        var model = new AvailabilityViewModel
        {
            Days = new List<DayAvailability>
        {
            new DayAvailability { Day = "Monday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Tuesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Wednesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Thursday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Friday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Saturday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Sunday", TimeSlots = new List<TimeSlot>() }
        }
        };

        try
        {
            var daysSnapshot = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .OnceAsync<Dictionary<string, dynamic>>();

            foreach (var dayEntry in daysSnapshot)
            {
                var dayName = dayEntry.Key;
                var timeSlotsForDay = dayEntry.Object["TimeSlots"];

                var dayAvailability = model.Days.FirstOrDefault(d => d.Day == dayName);

                if (dayAvailability != null && timeSlotsForDay != null)
                {
                    foreach (var timeSlotEntry in timeSlotsForDay)
                    {
                        var timeSlot = timeSlotEntry.Value;

                        if (timeSlot.ContainsKey("StartTime") && timeSlot.ContainsKey("EndTime"))
                        {
                            dayAvailability.TimeSlots.Add(new TimeSlot
                            {
                                StartTime = timeSlot["StartTime"],
                                EndTime = timeSlot["EndTime"]
                            });
                        }
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching raw availability: {ex.Message}");
        }

        return model;
    }

    public AvailabilityViewModel ConvertToHourlySegments(AvailabilityViewModel rawAvailability)
    {
        var hourlyAvailability = new AvailabilityViewModel
        {
            Days = rawAvailability.Days.Select(day => new DayAvailability
            {
                Day = day.Day,
                TimeSlots = new List<TimeSlot>()
            }).ToList()
        };

        foreach (var day in rawAvailability.Days)
        {
            var dayAvailability = hourlyAvailability.Days.FirstOrDefault(d => d.Day == day.Day);

            if (dayAvailability != null && day.TimeSlots != null)
            {
                foreach (var slot in day.TimeSlots)
                {
                    // Parse StartTime and EndTime
                    var startTime = DateTime.Parse(slot.StartTime);
                    var endTime = DateTime.Parse(slot.EndTime);

                    // Create hourly slots from startTime to endTime
                    while (startTime < endTime)
                    {
                        var nextHour = startTime.AddHours(1);
                        if (nextHour > endTime)
                            nextHour = endTime;

                        dayAvailability.TimeSlots.Add(new TimeSlot
                        {
                            StartTime = startTime.ToString("HH:mm"),
                            EndTime = nextHour.ToString("HH:mm")
                        });

                        startTime = nextHour; // Move to the next hour
                    }
                }
            }
        }

        return hourlyAvailability;
    }

    public async Task<Trainer> GetTrainerByIdAsync(string userId)
    {
        var user = await firebase
            .Child("users")
            .Child(userId)
            .OnceSingleAsync<dynamic>();

        if (user == null)
        {
            return null;
        }
        return new Trainer
        {
            Id = userId,
            Name = $"{user.firstName} {user.lastName}",
            ProfilePictureUrl = user.profileImageUrl,
            HourlyRate = user.rate,
            Email = user.email // Ensure this field is available in Firebase
        };
    }

    public async Task<List<Trainer>> GetAllTrainersAsync()
    {
        var trainers = new List<Trainer>();

        // Fetch all users from Firebase
        var users = await firebase
            .Child("users")
            .OnceAsync<dynamic>();

        // Filter users who are admins (trainers)
        foreach (var user in users)
        {
            if (user.Object.role == "admin") // Only add users with "admin" role
            {
                string profileImageUrl = user.Object.profileImageUrl ?? "/images/default.jpg";

                trainers.Add(new Trainer
                {
                    Id = user.Key,  // Set the trainer's Firebase ID
                    Name = $"{user.Object.firstName} {user.Object.lastName}",
                    ProfilePictureUrl = profileImageUrl,
                    HourlyRate = user.Object.rate
                });
            }
        }
        return trainers;
    }

    public async Task DeleteUserDataAsync(string userId)
    {
        try
        {
            await firebase
                .Child("users")
                .Child(userId)
                .DeleteAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error deleting user data: {ex.Message}");
            throw;
        }
    }

    public async Task<string> GetRateAsync(string userId)
    {
        try
        {
            var rate = await firebase
                .Child("users")
                .Child(userId)
                .Child("rate")
                .OnceSingleAsync<string>();

            return rate;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching rate: {ex.Message}");
            return null; // or handle the error as needed
        }
    }

    public async Task SaveRateAsync(string userId, string rate)
    {
        await firebase
            .Child("users")
            .Child(userId)
            .Child("rate")
            .PutAsync<string>(rate);
    }

    // Method to upload profile image and return its URL
    public async Task<string> UploadProfileImageAsync(string userId, IFormFile photo)
    {
        // Generate a unique file name
        var fileName = $"{userId}/profile-image.{photo.ContentType.Split('/')[1]}";

        // Upload the image to Firebase Storage
        var stream = photo.OpenReadStream();
        var imageUrl = await storage.Child("users").Child(fileName).PutAsync(stream);

        // Return the image URL
        return imageUrl;
    }

    // Method to save profile image URL to Firebase Realtime Database
    public async Task SaveProfileImageUrlAsync(string userId, string imageUrl)
    {
        try
        {
            // Ensure imageUrl is a valid string
            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new ArgumentException("Image URL cannot be null or empty.");
            }

            // Log the URL for debugging purposes
            Console.WriteLine($"Saving profile image URL: {imageUrl}");

            // Save the image URL under the specified userId
            await firebase
                .Child("users")
                .Child(userId)
                .Child("profileImageUrl")
                .PutAsync<string>(imageUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving profile image URL: {ex.Message}");
            throw;
        }
    }

    // Method to get the profile image URL for a user
    public async Task<string> GetProfileImageUrlAsync(string userId)
    {
        try
        {
            var imageUrl = await firebase
                .Child("users")
                .Child(userId)
                .Child("profileImageUrl")
                .OnceSingleAsync<string>();

            return imageUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching profile image URL: {ex.Message}");
            return null; // or you can return a default image URL if needed
        }
    }

    public async Task<AvailabilityViewModel> GetAvailabilityAsync(string userId)
    {
        var model = new AvailabilityViewModel
        {
            Days = new List<DayAvailability>
        {
            new DayAvailability { Day = "Monday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Tuesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Wednesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Thursday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Friday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Saturday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Sunday", TimeSlots = new List<TimeSlot>() }
        }
        };

        try
        {
            var daysSnapshot = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .OnceAsync<Dictionary<string, Dictionary<string, TimeSlot>>>();

            foreach (var dayEntry in daysSnapshot)
            {
                var dayAvailability = model.Days.FirstOrDefault(d => d.Day == dayEntry.Key);
                if (dayAvailability != null && dayEntry.Object != null)
                {
                    // Flatten all TimeSlot dictionaries into a single list
                    var timeSlots = new List<TimeSlot>();
                    foreach (var slotDict in dayEntry.Object.Values)
                    {
                        timeSlots.AddRange(slotDict.Values);
                    }
                    dayAvailability.TimeSlots = timeSlots;
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching availability: {ex.Message}");
            // Handle any exceptions and just return the default days with no time slots
        }

        return model;
    }

    public async Task SaveTimeSlotAsync(string day, string startTime, string endTime, string userId)
    {
        var timeSlot = new TimeSlot { StartTime = startTime, EndTime = endTime };
        await firebase
            .Child("users")
            .Child(userId)
            .Child("Days")
            .Child(day)
            .Child("TimeSlots")
            .PostAsync(timeSlot);
    }

    public async Task RemoveTimeSlotAsync(string day, string startTime, string endTime, string userId)
    {
        try
        {
            var slots = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .Child(day)
                .Child("TimeSlots")
                .OnceAsync<TimeSlot>();

            var slotToRemove = slots.FirstOrDefault(ts => ts.Object.StartTime == startTime && ts.Object.EndTime == endTime);

            if (slotToRemove != null)
            {
                Console.WriteLine("Deleting slot with Key: " + slotToRemove.Key);  // Log key to debug
                await firebase
                    .Child("users")
                    .Child(userId)
                    .Child("Days")
                    .Child(day)
                    .Child("TimeSlots")
                    .Child(slotToRemove.Key)
                    .DeleteAsync();
            }
            else
            {
                Console.WriteLine("Slot not found or already deleted");  // Log if not found
                throw new Exception("Time slot not found or already deleted.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing time slot: {ex.Message}");  // Log detailed error
            throw; // Re-throwing the exception to be caught by the calling method
        }
    }
}