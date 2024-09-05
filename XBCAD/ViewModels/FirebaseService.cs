using Firebase.Database;
using Firebase.Database.Query;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XBCAD.ViewModels;

public class FirebaseService
{
    private readonly FirebaseClient firebase;

    public FirebaseService()
    {
        // Ensure this URL is correct and points to your Firebase Realtime Database
        firebase = new FirebaseClient("https://alleysway-310a8-default-rtdb.firebaseio.com/");
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