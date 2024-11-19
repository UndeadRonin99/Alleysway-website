namespace XBCAD.ViewModels
{
    // The RegisterViewModel class is used to represent the data required for user registration.
    public class RegisterViewModel
    {
        // Stores the first name of the user.
        public string FirstName { get; set; }
        // Stores the last name of the user.
        public string LastName { get; set; }
        // Stores the username chosen by the user.
        public string Username { get; set; }
        // Stores the password entered by the user.
        public string Password { get; set; }
        // Stores the confirmation of the password to ensure it matches the Password field.
        public string ConfirmPassword { get; set; }
    }
}
