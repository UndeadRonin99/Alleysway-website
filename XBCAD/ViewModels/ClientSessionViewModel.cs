namespace XBCAD.ViewModels
{
    // Represents the view model for displaying information about a client session.
    public class ClientSessionViewModel
    {
        // The booked session details, including information such as trainer and client IDs, session times, etc.
        public BookedSession Session { get; set; }
        // The name of the trainer associated with the session.
        public string TrainerName { get; set; }
        // The URL of the trainer's profile image.
        public string TrainerProfileImageUrl { get; set; }
        // The name of the client who booked the session.
        public string ClientName { get; set; } // Add ClientName property

    }
}
