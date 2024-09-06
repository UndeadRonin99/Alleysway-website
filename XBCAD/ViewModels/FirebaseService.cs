﻿using Firebase.Database;
using Firebase.Database.Query;
using Firebase.Storage;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using XBCAD.ViewModels;

public class FirebaseService
{
    private readonly FirebaseClient firebase;
    private readonly FirebaseStorage storage;


    public FirebaseService()
    {
        // Ensure this URL is correct and points to your Firebase Realtime Database
        firebase = new FirebaseClient("https://alleysway-310a8-default-rtdb.firebaseio.com/");

        // Initialize Firebase Storage 
        storage = new FirebaseStorage("alleysway-310a8.appspot.com");
    }

    public async Task SaveRateAsync(string userId, string rate)
    {
        await firebase
            .Child("users")
            .Child(userId)
            .Child("rate")
            .PutAsync<string>(rate);
    }

    // Method to upload profile image and return its URL
    public async Task<string> UploadProfileImageAsync(string userId, IFormFile photo)
    {
        // Generate a unique file name
        var fileName = $"{userId}/profile-image.{photo.ContentType.Split('/')[1]}";

        // Upload the image to Firebase Storage
        var stream = photo.OpenReadStream();
        var imageUrl = await storage.Child("users").Child(fileName).PutAsync(stream);

        // Return the image URL
        return imageUrl;
    }

    // Method to save profile image URL to Firebase Realtime Database
    public async Task SaveProfileImageUrlAsync(string userId, string imageUrl)
    {
        try
        {
            // Ensure imageUrl is a valid string
            if (string.IsNullOrEmpty(imageUrl))
            {
                throw new ArgumentException("Image URL cannot be null or empty.");
            }

            // Log the URL for debugging purposes
            Console.WriteLine($"Saving profile image URL: {imageUrl}");

            // Save the image URL under the specified userId
            await firebase
                .Child("users")
                .Child(userId)
                .Child("profileImageUrl")
                .PutAsync<string>(imageUrl);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving profile image URL: {ex.Message}");
            throw;
        }
    }


    // Method to get the profile image URL for a user
    public async Task<string> GetProfileImageUrlAsync(string userId)
    {
        try
        {
            var imageUrl = await firebase
                .Child("users")
                .Child(userId)
                .Child("profileImageUrl")
                .OnceSingleAsync<string>();

            return imageUrl;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching profile image URL: {ex.Message}");
            return null; // or you can return a default image URL if needed
        }
    }


    public async Task<AvailabilityViewModel> GetAvailabilityAsync(string userId)
    {
        var model = new AvailabilityViewModel
        {
            Days = new List<DayAvailability>
        {
            new DayAvailability { Day = "Monday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Tuesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Wednesday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Thursday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Friday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Saturday", TimeSlots = new List<TimeSlot>() },
            new DayAvailability { Day = "Sunday", TimeSlots = new List<TimeSlot>() }
        }
        };

        try
        {
            var daysSnapshot = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .OnceAsync<Dictionary<string, Dictionary<string, TimeSlot>>>();

            foreach (var dayEntry in daysSnapshot)
            {
                var dayAvailability = model.Days.FirstOrDefault(d => d.Day == dayEntry.Key);
                if (dayAvailability != null && dayEntry.Object != null)
                {
                    // Flatten all TimeSlot dictionaries into a single list
                    var timeSlots = new List<TimeSlot>();
                    foreach (var slotDict in dayEntry.Object.Values)
                    {
                        timeSlots.AddRange(slotDict.Values);
                    }
                    dayAvailability.TimeSlots = timeSlots;
                }
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error fetching availability: {ex.Message}");
            // Handle any exceptions and just return the default days with no time slots
        }

        return model;
    }



    public async Task SaveTimeSlotAsync(string day, string startTime, string endTime, string userId)
    {
        var timeSlot = new TimeSlot { StartTime = startTime, EndTime = endTime };
        await firebase
            .Child("users")
            .Child(userId)
            .Child("Days")
            .Child(day)
            .Child("TimeSlots")
            .PostAsync(timeSlot);
    }


    public async Task RemoveTimeSlotAsync(string day, string startTime, string endTime, string userId)
    {
        try
        {
            var slots = await firebase
                .Child("users")
                .Child(userId)
                .Child("Days")
                .Child(day)
                .Child("TimeSlots")
                .OnceAsync<TimeSlot>();

            var slotToRemove = slots.FirstOrDefault(ts => ts.Object.StartTime == startTime && ts.Object.EndTime == endTime);

            if (slotToRemove != null)
            {
                Console.WriteLine("Deleting slot with Key: " + slotToRemove.Key);  // Log key to debug
                await firebase
                    .Child("users")
                    .Child(userId)
                    .Child("Days")
                    .Child(day)
                    .Child("TimeSlots")
                    .Child(slotToRemove.Key)
                    .DeleteAsync();
            }
            else
            {
                Console.WriteLine("Slot not found or already deleted");  // Log if not found
                throw new Exception("Time slot not found or already deleted.");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error removing time slot: {ex.Message}");  // Log detailed error
            throw; // Re-throwing the exception to be caught by the calling method
        }
    }

}