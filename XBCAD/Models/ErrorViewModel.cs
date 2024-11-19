namespace XBCAD.Models
{
    // The ErrorViewModel class is used to represent error-related data in the application.
    public class ErrorViewModel
    {
        // RequestId stores a unique identifier for the specific request where the error occurred.
        public string? RequestId { get; set; }
        
        // ShowRequestId is a boolean property that indicates whether the RequestId should be displayed.
        // It returns true if RequestId is not null or empty, otherwise false.
        public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
    }
}
