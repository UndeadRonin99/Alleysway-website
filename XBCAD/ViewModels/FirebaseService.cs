﻿
using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using Google.Cloud.Firestore;
using Newtonsoft.Json;
using XBCAD.ViewModels;

public class FirebaseService : IFirebaseService
{
    // Firebase Realtime Database client for interacting with the database.
    public readonly FirebaseClient firebase;
    // Firebase Storage client for handling file uploads and downloads.
    private readonly FirebaseStorage storage;

    // Constructor to initialize Firebase services.
    public FirebaseService()
    {
        // Ensure this URL is correct and points to your Firebase Realtime Database
        firebase = new FirebaseClient("https://alleysway-310a8-default-rtdb.firebaseio.com/");

        // Initialize Firebase Storage 
        storage = new FirebaseStorage("alleysway-310a8.appspot.com");
    }

    // Check if a user exists in Firebase Realtime Database by their email.
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
            // Log any errors that occur.
            Console.WriteLine($"Error checking user by email: {ex.Message}");
        }

        // Return null if no user is found or an error occurred
        return null;
    }


    // Fetch a payment summary for a specific client, including payments to their trainers.
    public async Task<ClientPaymentSummaryViewModel> GetClientPaymentSummaryAsync(string clientId)
    {
        var payments = new List<TrainerPayment>();

        // Fetch all sessions for the specified client.
        var sessions = await firebase
            .Child("users")
            .Child(clientId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        // Group paid sessions by trainer.
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

            // Add the payment details to the list.
            payments.Add(new TrainerPayment
            {
                TrainerId = trainerId,
                TrainerName = trainerName,
                TotalAmountPaid = totalAmountPaid
            });
        }

        // Return the payment summary.
        return new ClientPaymentSummaryViewModel
        {
            Payments = payments
        };
    }


    // Update the payment status of a specific session.
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
    // Fetch all sessions between a specific trainer and client.
    public async Task<List<BookedSession>> GetSessionsBetweenTrainerAndClientAsync(string trainerId, string clientId)
    {
        var sessions = new List<BookedSession>();

        // Fetch all sessions for the trainer.
        var sessionNodes = await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        foreach (var sessionNode in sessionNodes)
        {
            var session = sessionNode.Object;
            // Check if the session involves the specified client.
            if (session.ClientID == clientId)
            {
                session.SessionKey = sessionNode.Key; // Store the key
                sessions.Add(session);
            }
        }

        return sessions;
    }

    // Fetch client details by their unique ID.
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
    // Fetch a list of trainers for a specific client.
    public async Task<List<Trainer>> GetTrainersForClientAsync(string clientId)
    {
        var trainers = new List<Trainer>();

        // Get all sessions for the client
        var sessions = await firebase
            .Child("users")
            .Child(clientId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        // Extract unique trainer IDs
        var trainerIds = sessions.Select(s => s.Object.TrainerID).Distinct();

        foreach (var trainerId in trainerIds)
        {
            // Get trainer details
            var trainerData = await firebase
                .Child("users")
                .Child(trainerId)
                .OnceSingleAsync<dynamic>();

            if (trainerData != null)
            {
                string profileImageUrl = trainerData.profileImageUrl ?? "/images/default.jpg";
                trainers.Add(new Trainer
                {
                    Id = trainerId,
                    Name = $"{trainerData.firstName} {trainerData.lastName}",
                    ProfilePictureUrl = profileImageUrl
                });
            }
        }

        return trainers;
    }

    // Fetch all clients for a specific trainer.
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

    // Create and store a booked session for both the trainer and client.
    public async Task PutBookedSession(BookedSession session, string trainerID, string userID, string userName, DateTime dateTime)
    {
        // Generate a unique session ID
        var sessionId = Guid.NewGuid().ToString();

        // Assign the session ID to the session object
        session.SessionKey = sessionId;

        // Save the session under the same session ID for both the trainer and client
        await firebase
            .Child("users")
            .Child(trainerID)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .PutAsync(session);

        // Save the same session under the client's sessions in Firebase.
        await firebase
            .Child("users")
            .Child(userID)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .PutAsync(session);

        // Send a message to the trainer
        Message clientMessage = new Message
        {
            senderId = userID,
            receiverId = trainerID,
            senderName = userName,
            text = $"I've booked a session with you on {dateTime:dd/MM/yyyy} at {dateTime.AddHours(2):HH:mm}",
            timestamp = Timestamp.GetCurrentTimestamp()
        };

        // Save the message for both client and trainer
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


    // Fetch raw availability data for a specific user.
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
            // Fetch each day's availability data from Firebase.
            var daysSnapshot = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .OnceAsync<Dictionary<string, dynamic>>();

            foreach (var dayEntry in daysSnapshot)
            {
                var dayName = dayEntry.Key;
                var timeSlotsForDay = dayEntry.Object["TimeSlots"];

                // Match the day in the model with the data from Firebase.
                var dayAvailability = model.Days.FirstOrDefault(d => d.Day == dayName);

                if (dayAvailability != null && timeSlotsForDay != null)
                {
                    foreach (var timeSlotEntry in timeSlotsForDay)
                    {
                        var timeSlot = timeSlotEntry.Value;

                        // Add time slots to the availability model.
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
            // Log errors during availability fetching.
            Console.WriteLine($"Error fetching raw availability: {ex.Message}");
        }

        return model;
    }

    // Convert raw availability data into hourly segments.
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

    // Fetch trainer details by their unique ID.
    public async Task<Trainer> GetTrainerByIdAsync(string userId)
    {
        var user = await firebase
            .Child("users")
            .Child(userId)
            .OnceSingleAsync<dynamic>();

        if (user == null)
        {
            return null; // Return null if no user data is found.
        }

        return new Trainer
        {
            Id = userId,
            Name = $"{user.firstName} {user.lastName}",
            ProfilePictureUrl = user.profileImageUrl,
            HourlyRate = user.rate != null ? (int)user.rate : 0, // Default to 0 if rate is null
            Email = user.email
        };
    }


    // Fetch all trainers from the database.
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

                // Handle potential null rate by providing a default value, e.g., 0
                int hourlyRate = user.Object.rate != null ? (int)user.Object.rate : 0;

                trainers.Add(new Trainer
                {
                    Id = user.Key,  // Set the trainer's Firebase ID
                    Name = $"{user.Object.firstName} {user.Object.lastName}",
                    ProfilePictureUrl = profileImageUrl,
                    HourlyRate = hourlyRate
                });
            }
        }
        return trainers;
    }


    // Delete a user's data from Firebase.
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
            // Log any errors during the deletion process.
            Console.WriteLine($"Error deleting user data: {ex.Message}");
            throw;
        }
    }

    // Fetch the hourly rate for a specific user.
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
            // Log any errors during rate fetching.
            Console.WriteLine($"Error fetching rate: {ex.Message}");
            return null; // or handle the error as needed
        }
    }

    // Save or update the hourly rate for a specific user.
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

    // Fetch availability data for a specific user.
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
            // Fetch availability data for each day from Firebase.
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

    // Save a new time slot for a specific day and user.
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

    // Remove a specific time slot for a user on a specific day.
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

            // Find the specific slot to remove.
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
                // Log if the slot is not found.
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

    // Fetch future booked sessions for a trainer.
    public async Task<List<BookedSession>> GetFutureBookedSessionsForTrainerAsync(string trainerId, DateTime currentDateTime)
    {
        var sessions = new List<BookedSession>();

        // Fetch all sessions for the trainer.
        var sessionNodes = await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        foreach (var sessionNode in sessionNodes)
        {
            var session = sessionNode.Object;
            // Add sessions that are scheduled for the future.
            if (DateTime.Parse(session.StartDateTime) >= currentDateTime)
            {
                sessions.Add(session);
            }
        }

        return sessions;
    }
    // Fetch all sessions for a specific client and include trainer details.
    public async Task<List<ClientSessionViewModel>> GetClientSessionsAsync(string clientId)
    {
        var sessionsList = new List<ClientSessionViewModel>();

        // Get all sessions for the client
        var sessions = await firebase
            .Child("users")
            .Child(clientId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();
        foreach (var sessionNode in sessions)
        {
            var bookedSession = sessionNode.Object;
            bookedSession.SessionKey = sessionNode.Key; // Save the session key if needed

            // Check if TrainerID is null or empty
            if (string.IsNullOrEmpty(bookedSession.TrainerID))
            {
                // Optionally log the issue
                Console.WriteLine($"Session {bookedSession.SessionKey} has a null or empty TrainerID.");
                continue; // Skip to the next session
            }

            // Get trainer details
            var trainerData = await firebase
                .Child("users")
                .Child(bookedSession.TrainerID)
                .OnceSingleAsync<dynamic>();

            if (trainerData != null)
            {
                string trainerName = $"{trainerData.firstName} {trainerData.lastName}";
                string trainerProfileImageUrl = trainerData.profileImageUrl ?? "/images/default.jpg";

                // Create a ViewModel to hold session and trainer details
                var clientSessionViewModel = new ClientSessionViewModel
                {
                    Session = bookedSession,
                    TrainerName = trainerName,
                    TrainerProfileImageUrl = trainerProfileImageUrl
                };

                sessionsList.Add(clientSessionViewModel);
            }
        }

        return sessionsList;
    }
    // Cancel a session for a client and notify the trainer.
    public async Task CancelSessionAsync(string clientId, string trainerId, string sessionId, string clientName, BookedSession session)
    {
        // Delete the session for the client
        await firebase
            .Child("users")
            .Child(clientId)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .DeleteAsync();

        // Delete the session for the trainer
        await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .DeleteAsync();

        // Send a message to the trainer about the cancellation
        var sessionStartTime = DateTime.Parse(session.StartDateTime);

        Message cancellationMessage = new Message
        {
            senderId = clientId,
            receiverId = trainerId,
            senderName = clientName,
            text = $"{clientName} has canceled the session on {sessionStartTime:dd/MM/yyyy} at {sessionStartTime:HH:mm}. Please check your emails for further information.",
            timestamp = Timestamp.GetCurrentTimestamp()
        };

        // Save the cancellation message for both client and trainer.
        await firebase
            .Child("user_messages")
            .Child(clientId)
            .Child(trainerId)
            .Child("messages")
            .PostAsync(cancellationMessage);

        await firebase
            .Child("user_messages")
            .Child(trainerId)
            .Child(clientId)
            .Child("messages")
            .PostAsync(cancellationMessage);
    }

    // Save date-specific unavailability for a user.
    public async Task<string> SaveDateSpecificUnavailabilityAsync(string userId, string date, string startTime, string endTime, bool isFullDayUnavailable)
    {
        var timeSlot = new TimeSlot
        {
            StartTime = startTime,
            EndTime = endTime,
            IsFullDayUnavailable = isFullDayUnavailable
        };

        // Save timeSlot under the specific date with a unique identifier
        var slotReference = await firebase
            .Child("users")
            .Child(userId)
            .Child("DateSpecificAvailability")
            .Child(date)
            .PostAsync(timeSlot);

        // Return the slotId (key of the saved time slot)
        return slotReference.Key;
    }

    // Save date-specific availability for a user.
    public async Task<string> SaveDateSpecificAvailabilityAsync(string userId, string date, string startTime, string endTime)
    {
        var timeSlot = new TimeSlot
        {
            StartTime = startTime,
            EndTime = endTime,
        };

        // Save timeSlot under the specific date with a unique identifier
        var slotReference = await firebase
            .Child("users")
            .Child(userId)
            .Child("DateSpecificAvailability")
            .Child(date)
            .PostAsync(timeSlot);

        // Return the slotId (key of the saved time slot)
        return slotReference.Key;
    }



    // Fetch all date-specific availability for a user.
    public async Task<Dictionary<string, List<DateSpecificTimeSlot>>> GetAllDateSpecificAvailabilityAsync(string userId)
    {
        var dateSpecificAvailability = new Dictionary<string, List<DateSpecificTimeSlot>>();

        // Fetch all date nodes under "DateSpecificAvailability"
        var dateNodes = await firebase
            .Child("users")
            .Child(userId)
            .Child("DateSpecificAvailability")
            .OnceAsync<object>(); // Fetches each date node

        foreach (var dateNode in dateNodes)
        {
            var date = dateNode.Key;

            // Initialize the list for this date if it doesn't exist
            if (!dateSpecificAvailability.ContainsKey(date))
            {
                dateSpecificAvailability[date] = new List<DateSpecificTimeSlot>();
            }

            // Fetch all slots under this specific date
            var slots = await firebase
                .Child("users")
                .Child(userId)
                .Child("DateSpecificAvailability")
                .Child(date)
                .OnceAsync<TimeSlot>();

            foreach (var slot in slots)
            {
                // Create DateSpecificTimeSlot and set its Id, start time, end time, and availability flag
                var dateSpecificSlot = new DateSpecificTimeSlot
                {
                    Id = slot.Key,
                    StartTime = slot.Object.StartTime,
                    EndTime = slot.Object.EndTime,
                    IsFullDayUnavailable = slot.Object.IsFullDayUnavailable
                };

                // Add the slot to the list for this date
                dateSpecificAvailability[date].Add(dateSpecificSlot);
            }
        }

        return dateSpecificAvailability;
    }




    // Fetch all availability for a user across all days.
    public async Task<List<DayAvailability>> GetAllAvailabilityAsync(string userId)
    {
        var days = new List<DayAvailability>
    {
        new DayAvailability { Day = "Monday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Tuesday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Wednesday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Thursday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Friday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Saturday", TimeSlots = new List<TimeSlot>() },
        new DayAvailability { Day = "Sunday", TimeSlots = new List<TimeSlot>() }
    };

        // Fetch each day’s time slots from Firebase as individual TimeSlot objects
        foreach (var day in days)
        {
            var timeSlotsSnapshot = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .Child(day.Day)
                .Child("TimeSlots")
                .OnceAsync<TimeSlot>();

            foreach (var timeSlotEntry in timeSlotsSnapshot)
            {
                day.TimeSlots.Add(timeSlotEntry.Object);  // Add each TimeSlot to the day's list
            }
        }

        return days;
    }

    // Fetch date-specific availability for a user for a specific date.
    public async Task<List<TimeSlot>> GetDateSpecificAvailabilityAsync(string userId, string date)
    {
        var timeSlots = new List<TimeSlot>();

        // Fetch all time slots for the given date.
        var slots = await firebase
            .Child("users")
            .Child(userId)
            .Child("DateSpecificAvailability")
            .Child(date)
            .OnceAsync<TimeSlot>();

        // Add each slot to the list.
        foreach (var slot in slots)
        {
            timeSlots.Add(slot.Object);
        }

        return timeSlots;
    }

    // Remove a specific date-specific time slot for a user.
    public async Task<bool> RemoveDateSpecificTimeSlotAsync(string userId, string date, string slotId)
    {
        try
        {
            // Delete the specific slot from Firebase.
            await firebase
                .Child("users")
                .Child(userId)
                .Child("DateSpecificAvailability")
                .Child(date)
                .Child(slotId)  // Only delete the specific slot
                .DeleteAsync();

            Console.WriteLine("Deletion successful.");
            return true;
        }
        catch (Exception ex)
        {
            // Log errors during the deletion process.
            Console.WriteLine($"Error deleting slot: {ex.Message}");
            return false;
        }
    }


    // Fetch a specific session by user ID and session ID.
    public async Task<BookedSession> GetSessionAsync(string userId, string sessionId)
    {
        // Fetch the session data from Firebase.
        var session = await firebase
            .Child("users")
            .Child(userId)
            .Child("sessions")
            .Child("SessionID")
            .Child(sessionId)
            .OnceSingleAsync<BookedSession>();

        // Assign the session key if found.
        if (session != null)
        {
            session.SessionKey = sessionId;
        }

        return session;
    }

    // Fetch a list of message contacts for a user.
    public async Task<List<string>> GetMessageContactsAsync(string userId)
    {
        var contacts = new List<string>();
        try
        {
            // Fetch all message nodes for the user.
            var messagesRef = await firebase
                .Child("user_messages")
                .Child(userId)
                .OnceAsync<object>(); // We don't care about the message content here

            // Add each contact's user ID to the list.
            foreach (var messageNode in messagesRef)
            {
                contacts.Add(messageNode.Key); // The key is the contact userId
            }
        }
        catch (Exception ex)
        {
            // Log errors during the fetching process.
            Console.WriteLine($"Error fetching message contacts: {ex.Message}");
        }
        return contacts;
    }

    // Fetch details of trainers by a list of trainer IDs.
    public async Task<List<Trainer>> GetTrainersByIdsAsync(List<string> trainerIds)
    {
        var trainers = new List<Trainer>();
        foreach (var trainerId in trainerIds)
        {
            // Fetch trainer data for each ID.
            var trainerData = await firebase
                .Child("users")
                .Child(trainerId)
                .OnceSingleAsync<dynamic>();

            // Ensure the user has a trainer role.
            if (trainerData != null && trainerData.role == "admin") // Assuming trainers have role "admin"
            {
                var trainer = new Trainer
                {
                    Id = trainerId,
                    Name = $"{trainerData.firstName} {trainerData.lastName}",
                    ProfilePictureUrl = trainerData.profileImageUrl ?? "/images/default.jpg",
                    // Add other properties as needed
                };
                trainers.Add(trainer);
            }
        }
        return trainers;
    }

    // Fetch details of clients by a list of client IDs.
    public async Task<List<ClientViewModel>> GetClientsByIdsAsync(List<string> clientIds)
    {
        var clients = new List<ClientViewModel>();
        foreach (var clientId in clientIds)
        {
            // Fetch client data for each ID.
            var clientData = await firebase
                .Child("users")
                .Child(clientId)
                .OnceSingleAsync<dynamic>();

            // Ensure the user is not an admin (client role).
            if (clientData != null && clientData.role != "admin") // Assuming clients don't have role "admin"
            {
                var client = new ClientViewModel
                {
                    Id = clientId,
                    Name = $"{clientData.firstName} {clientData.lastName}",
                    ProfileImageUrl = clientData.profileImageUrl ?? "/images/default.jpg",
                    // Add other properties as needed
                };
                clients.Add(client);
            }
        }
        return clients;
    }

    // Fetch all sessions for a trainer, grouped by clients.
    public async Task<List<ClientSessionsViewModel>> GetAllSessionsForTrainerAsync(string trainerId)
    {
        var sessionsList = new List<ClientSessionsViewModel>();

        // Fetch all session nodes under the trainer's "sessions" path in Firebase
        var clientSessions = await firebase
            .Child("users")
            .Child(trainerId)
            .Child("sessions")
            .Child("SessionID")
            .OnceAsync<BookedSession>();

        // Group sessions by ClientID
        var clientGroupedSessions = clientSessions.GroupBy(s => s.Object.ClientID);

        foreach (var clientSessionGroup in clientGroupedSessions)
        {
            var clientId = clientSessionGroup.Key;

            // Fetch client data
            var clientData = await firebase
                .Child("users")
                .Child(clientId)
                .OnceSingleAsync<dynamic>();

            if (clientData == null)
            {
                continue; // Skip if client data is missing
            }

            // Extract session information for each client
            var sessions = clientSessionGroup.Select(s => s.Object).ToList();

            // Calculate total amount due and paid
            decimal totalAmountDue = sessions.Where(s => !s.Paid).Sum(s => s.TotalAmount);
            decimal totalAmountPaid = sessions.Where(s => s.Paid).Sum(s => s.TotalAmount);

            // Create client view model
            var clientViewModel = new ClientViewModel
            {
                Id = clientId,
                Name = $"{clientData.firstName} {clientData.lastName}",
                ProfileImageUrl = clientData.profileImageUrl ?? "/images/default.jpg"
            };

            // Add to sessions list
            sessionsList.Add(new ClientSessionsViewModel
            {
                Client = clientViewModel,
                Sessions = sessions,
                TotalAmountDue = totalAmountDue,
                TotalAmountPaid = totalAmountPaid
            });
        }

        return sessionsList;
    }

    // Fetch all clients from Firebase.
    public async Task<List<ClientViewModel>> GetAllClientsAsync()
    {
        var clients = new List<ClientViewModel>();

        // Fetch all users from Firebase
        var users = await firebase
            .Child("users")
            .OnceAsync<dynamic>();

        // Filter out clients (assuming that clients are not "admin" role)
        foreach (var user in users)
        {
            if (user.Object.role != "admin") // Assuming clients have role other than "admin"
            {
                string profileImageUrl = user.Object.profileImageUrl ?? "/images/default.jpg";

                clients.Add(new ClientViewModel
                {
                    Id = user.Key,
                    Name = $"{user.Object.firstName} {user.Object.lastName}",
                    ProfileImageUrl = profileImageUrl
                });
            }
        }

        return clients;
    }


}
