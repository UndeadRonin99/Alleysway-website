namespace XBCAD.ViewModels
{
    public class AvailabilityViewModel
    {
        public List<DayAvailability> Days { get; set; }
    }

    public class DayAvailability
    {
        public string Day { get; set; }
        public List<TimeSlot> TimeSlots { get; set; }
    }

    public class TimeSlot
    {
        public string StartTime { get; set; }
        public string EndTime { get; set; }
    }
}
