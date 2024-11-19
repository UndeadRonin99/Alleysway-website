namespace XBCAD.ViewModels
{
    // Represents a summary of client payments made to trainers.
    public class ClientPaymentSummaryViewModel
    {
        // A list of payments made to trainers, where each item contains details about a specific trainer's payment summary.
        public List<TrainerPayment> Payments { get; set; }
    }

    // Represents the payment details for a specific trainer.
    public class TrainerPayment
    {
        // The unique identifier of the trainer.
        public string TrainerId { get; set; }
        // The name of the trainer.
        public string TrainerName { get; set; }
        // The total amount paid to this trainer.
        public decimal TotalAmountPaid { get; set; }
    }
}
