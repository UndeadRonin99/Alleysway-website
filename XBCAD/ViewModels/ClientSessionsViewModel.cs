namespace XBCAD.ViewModels
{
    // Represents the view model for displaying a summary of a client's sessions and payments.
    public class ClientSessionsViewModel
    {
        // Details of the client associated with these sessions.
        public ClientViewModel Client { get; set; }    
        // A list of booked sessions with the client.// Client details
        public List<BookedSession> Sessions { get; set; }   
        // The total amount due for unpaid sessions with the client.
        public decimal TotalAmountDue { get; set; }         
        // The total amount paid by the client.
        public decimal TotalAmountPaid { get; set; }  
        // The total number of sessions with the client, calculated from the count of the Sessions list.
        public int TotalSessions => Sessions.Count;         

    }
}
