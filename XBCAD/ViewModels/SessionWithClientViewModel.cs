// In ViewModels/SessionWithClientViewModel.cs
namespace XBCAD.ViewModels
{
    // ViewModel class for representing a session along with the associated client.
    public class SessionWithClientViewModel
    {
        // Represents the details of the booked session.
        public BookedSession Session { get; set; }
         // Represents the client associated with the session.
        public ClientViewModel Client { get; set; }
    }
}
