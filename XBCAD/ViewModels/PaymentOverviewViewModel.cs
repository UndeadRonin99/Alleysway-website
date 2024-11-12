namespace XBCAD.ViewModels
{
    public class PaymentOverviewViewModel
    {
        public List<ClientSessionsViewModel> Sessions { get; set; } // Updated to ClientSessionsViewModel

        // Analytics properties
        public int TotalSessions => Sessions.Sum(s => s.TotalSessions);
        public decimal TotalIncome => Sessions.Sum(s => s.TotalAmountPaid);
        public int TotalUnpaidSessions => Sessions.Sum(s => s.Sessions.Count(sess => !sess.Paid));
    }
}
