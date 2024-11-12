namespace XBCAD.ViewModels
{
    public class ClientSessionsViewModel
    {
        public ClientViewModel Client { get; set; }         // Client details
        public List<BookedSession> Sessions { get; set; }   // List of sessions with this client
        public decimal TotalAmountDue { get; set; }         // Total amount due (unpaid sessions)
        public decimal TotalAmountPaid { get; set; }        // Total amount paid
        public int TotalSessions => Sessions.Count;         // Total number of sessions for the client

    }
}
