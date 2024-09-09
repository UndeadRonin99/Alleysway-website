namespace XBCAD.ViewModels
{
    public class Trainer
    {
        public string Id { get; set; }  // Ensure this property exists to fetch specific data
        public string Name { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string HourlyRate { get; set; }
        public string Role { get; set; }
    }
    public class TrainerAvailabilityViewModel
    {
        public Trainer Trainer { get; set; } = default!;  // The selected trainer
        public AvailabilityViewModel Availability { get; set; } = default!;  // Their availability
    }

}
