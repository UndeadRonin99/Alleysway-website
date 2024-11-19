namespace XBCAD.ViewModels
{
    // ViewModel class for displaying a trainer's sessions in the application.
    public class TrainerSessionsViewModel
    {
        // Represents the trainer whose sessions are being managed or displayed.
        public Trainer Trainer { get; set; }
        // List of booked sessions for the trainer.
        // Each session is an instance of the BookedSession class that contains session-specific details.
        public List<BookedSession> Sessions { get; set; }
    }


}
