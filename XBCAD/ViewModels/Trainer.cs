namespace XBCAD.ViewModels
{
    public class Trainer
    {
        public string Id { get; set; }  // Ensure this property exists to fetch specific data
        public string Name { get; set; }
        public string ProfilePictureUrl { get; set; }
        public int HourlyRate { get; set; }
        public string Role { get; set; }
        public string Email { get; set; } // Ensure Email property exists
    }

    public class TrainerAvailabilityViewModel
    {
        public Trainer Trainer { get; set; } = default!;  // The selected trainer

        public AvailabilityViewModel Availability { get; set; } = default!;  // General weekly availability

        // New property to hold date-specific availability
        public Dictionary<string, List<TimeSlot>> DateSpecificAvailability { get; set; } = new Dictionary<string, List<TimeSlot>>();

        // Capture selected time slots
        public List<SelectedTimeSlot> SelectedTimeSlots { get; set; } = new List<SelectedTimeSlot>();
    }

    // Define SelectedTimeSlot class
    public class SelectedTimeSlot
    {
        public string Date { get; set; } = default!;  // Date in yyyy-MM-dd format
        public string StartTime { get; set; } = default!;
        public string EndTime { get; set; } = default!;
    }
}
