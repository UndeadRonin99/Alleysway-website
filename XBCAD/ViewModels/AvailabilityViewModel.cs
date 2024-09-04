namespace XBCAD.ViewModels
{
    public class AvailabilityViewModel
    {
        public List<DayAvailability> Days { get; set; }
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
    }
}