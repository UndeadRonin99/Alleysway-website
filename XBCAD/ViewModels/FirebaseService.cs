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

    public async Task<AvailabilityViewModel> GetAvailabilityAsync()
    {
        // Initialize the model with default days (Monday to Sunday)
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
            // Fetch data from Firebase and store it in a nested dictionary
            var daysSnapshot = await firebase
                .Child("Days")
                .OnceAsync<Dictionary<string, Dictionary<string, TimeSlot>>>();

            // If there is data, update the corresponding days in the model
            foreach (var dayEntry in daysSnapshot)
            {
                var dayAvailability = model.Days.FirstOrDefault(d => d.Day == dayEntry.Key);
                if (dayAvailability != null)
                {
                    // Flatten the nested dictionary into a list of TimeSlot objects
                    dayAvailability.TimeSlots = dayEntry.Object.Values.SelectMany(dict => dict.Values).ToList();
                }
            }
        }
        catch
        {
            // In case of any exception (e.g., Firebase data fetch fails), just return the default days with no time slots
        }

        return model;
    }



    public async Task SaveTimeSlotAsync(string day, string startTime, string endTime)
    {
        var timeSlot = new TimeSlot { StartTime = startTime, EndTime = endTime };
        await firebase
            .Child("Days")
            .Child(day)
            .Child("TimeSlots")
            .PostAsync(timeSlot);
    }

    public async Task RemoveTimeSlotAsync(string day, string startTime, string endTime)
    {
        var slots = await firebase
            .Child("Days")
            .Child(day)
            .Child("TimeSlots")
            .OnceAsync<TimeSlot>();

        var slotToRemove = slots.FirstOrDefault(ts => ts.Object.StartTime == startTime && ts.Object.EndTime == endTime);
        if (slotToRemove != null)
        {
            await firebase
                .Child("Days")
                .Child(day)
                .Child("TimeSlots")
                .Child(slotToRemove.Key)
                .DeleteAsync();
        }
    }
}