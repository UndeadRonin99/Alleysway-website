namespace XBCAD.ViewModels;

public class AvailabilityViewModel
{
    public string UserId { get; set; }
    public List<DayAvailability> Days { get; set; }
    public Dictionary<string, List<DateSpecificTimeSlot>> DateSpecificAvailability { get; set; } = new Dictionary<string, List<DateSpecificTimeSlot>>();
}

public class DateSpecificTimeSlot
{
    public string Id { get; set; } // unique ID for each timeslot
    public string StartTime { get; set; }
    public string EndTime { get; set; }
    public bool IsFullDayUnavailable { get; set; }
}


public class DayAvailability
{
    public string Day { get; set; } = default!;  // Using default! to satisfy the compiler for non-nullable properties
    public List<TimeSlot> TimeSlots { get; set; } = new List<TimeSlot>();

}

public class TimeSlot
{
    public string StartTime { get; set; } = default!;
    public string EndTime { get; set; } = default!;
    public bool IsFullDayUnavailable { get; set; } = false; // New flag for full-day unavailability
}