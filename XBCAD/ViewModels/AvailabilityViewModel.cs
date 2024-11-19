namespace XBCAD.ViewModels;

// Represents the view model for user availability, including daily and date-specific availability.
public class AvailabilityViewModel
{
    // The ID of the user whose availability is being represented.
    public string UserId { get; set; }
    // A list representing availability for each day of the week (e.g., Monday, Tuesday, etc.).
    public List<DayAvailability> Days { get; set; }
    // A dictionary to store specific availability for certain dates.
    // The key is the date (as a string), and the value is a list of time slots for that date.
    public Dictionary<string, List<DateSpecificTimeSlot>> DateSpecificAvailability { get; set; } = new Dictionary<string, List<DateSpecificTimeSlot>>();
}

// Represents a specific time slot for a particular date.
public class DateSpecificTimeSlot
{
    // A unique identifier for the time slot.
    public string Id { get; set; } // unique ID for each timeslot
    // The start time of the time slot (e.g., "08:00 AM").
    public string StartTime { get; set; }
    // The end time of the time slot (e.g., "05:00 PM").
    public string EndTime { get; set; }
    // Indicates whether the entire day is marked as unavailable.
    public bool IsFullDayUnavailable { get; set; }
}

// Represents the availability for a specific day of the week.
public class DayAvailability
{
    // The name of the day (e.g., "Monday", "Tuesday").
    public string Day { get; set; } = default!;  // Using default! to satisfy the compiler for non-nullable properties
    // A list of time slots available for this day.
    public List<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();

}

// Represents a specific time slot within a day.
public class TimeSlot
{
    // The start time of the time slot (e.g., "09:00 AM").
    public string StartTime { get; set; } = default!;
    // The end time of the time slot (e.g., "12:00 PM").
    public string EndTime { get; set; } = default!;
    // Indicates whether the entire day is marked as unavailable for this specific time slot.
    public bool IsFullDayUnavailable { get; set; } = false; // New flag for full-day unavailability
}
