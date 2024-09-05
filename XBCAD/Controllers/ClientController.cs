using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace XBCAD.Controllers
{
    public class ClientController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Client Dashboard"; 
            //var userID = User.FindFirstValue(ClaimTypes.NameIdentifier); //Retrieve uid
            var Name = User.FindFirstValue(ClaimTypes.Name); //Retrieve Name
            ViewBag.Name = Name;
            return View();
        }

        public IActionResult Profile()
        {
            ViewData["Title"] = "Profile";
            return View();
        }
       
    }
}
