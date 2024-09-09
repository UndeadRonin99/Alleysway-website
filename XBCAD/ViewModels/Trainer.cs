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
    public class PersonalTrainer
    {
        public string Id { get; set; }
        public string FirstName { get; set; }
        public string LastName { get; set; }
        public string ProfilePictureUrl { get; set; }
        public string HourlyRate { get; set; }
    }
}
