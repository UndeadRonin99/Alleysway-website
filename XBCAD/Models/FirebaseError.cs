namespace XBCAD.Models
{
    // Represents the main structure for Firebase errors returned from the Firebase API.
    public class FirebaseError
    {
        // The error object contains detailed information about the Firebase error.
        public Error error { get; set; }
    }

    // Represents detailed information about an error.
    public class Error
    {
        // The HTTP status code or specific error code associated with the error.
        public int code { get; set; }
        // A descriptive message explaining the error.
        public string message { get; set; }
        // A list of additional error objects for cases where multiple errors are returned.
        public List<Error> errors { get; set; }
    }
}
