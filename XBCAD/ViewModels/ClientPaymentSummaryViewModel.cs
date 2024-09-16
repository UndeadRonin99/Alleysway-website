namespace XBCAD.ViewModels
{
    public class ClientPaymentSummaryViewModel
    {
        public List<TrainerPayment> Payments { get; set; }
    }

    public class TrainerPayment
    {
        public string TrainerId { get; set; }
        public string TrainerName { get; set; }
        public decimal TotalAmountPaid { get; set; }
    }
}
