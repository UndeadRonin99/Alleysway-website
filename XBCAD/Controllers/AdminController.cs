using Microsoft.AspNetCore.Mvc;
using XBCAD.ViewModels;
using System.Collections.Generic;

namespace XBCAD.Controllers
{
    public class AdminController : Controller
    {
       

        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Admin Dashboard";
            return View();
        }

        public IActionResult Users()
        {
            ViewData["Title"] = "Manage Users";
            return View();
        }

        private static AvailabilityViewModel savedAvailability = new AvailabilityViewModel
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

        public IActionResult Availability()
        {
            return View(savedAvailability);
        }

        [HttpPost]
        public IActionResult SaveTimeSlot(string day, string startTime, string endTime)
        {
            var savedDay = savedAvailability.Days.FirstOrDefault(d => d.Day == day);
            if (savedDay != null)
            {
                // Add the new time slot to the day's availability
                savedDay.TimeSlots.Add(new TimeSlot { StartTime = startTime, EndTime = endTime });
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public IActionResult RemoveTimeSlot(string day, string startTime, string endTime)
        {
            var savedDay = savedAvailability.Days.FirstOrDefault(d => d.Day == day);
            if (savedDay != null)
            {
                // Find and remove the time slot from the day's availability
                var slotToRemove = savedDay.TimeSlots.FirstOrDefault(ts => ts.StartTime == startTime && ts.EndTime == endTime);
                if (slotToRemove != null)
                {
                    savedDay.TimeSlots.Remove(slotToRemove);
                }
            }

            return Json(new { success = true });
        }
    }
}
