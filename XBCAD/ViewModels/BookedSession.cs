public class BookedSession
{
    // Represents the unique identifier for the trainer involved in the session.
    public string TrainerID { get; set; }
    // Represents the unique identifier for the client booking the session.
    public string ClientID { get; set; }
    // Indicates whether the session has been paid for.
    public bool Paid { get; set; }
     // Represents the total amount to be paid for the session.
    public decimal TotalAmount { get; set; }
     // The start date and time of the session, stored as a string in ISO 8601 format (e.g., "2024-11-19T10:00:00Z").
    public string StartDateTime { get; set; }  // As string in ISO 8601 format
      // The end date and time of the session, stored as a string in ISO 8601 format (e.g., "2024-11-19T11:00:00Z").
    public string EndDateTime { get; set; }    // As string in ISO 8601 format
     // A unique key used to identify the session.
    public string SessionKey { get; set; }
     // Represents the ID of the event associated with this booked session.
    public string EventId { get; set; } // Add this property
}









