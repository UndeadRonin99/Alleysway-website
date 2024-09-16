
public class BookedSession
{
    public string TrainerID { get; set; }
    public string ClientID { get; set; }
    public bool Paid { get; set; }
    public decimal TotalAmount { get; set; }
    public string StartDateTime { get; set; }  // As string in ISO 8601 format
    public string EndDateTime { get; set; }    // As string in ISO 8601 format
    public string SessionKey { get; set; }  

}


