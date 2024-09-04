using Microsoft.AspNetCore.Mvc;

namespace XBCAD.Controllers
{
    public class ClientController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Client Dashboard";
            ViewBag.FirstName = TempData["FirstName"]?.ToString();
            ViewBag.LastName = TempData["LastName"]?.ToString();
            return View();
        }

        public IActionResult Profile()
        {
            ViewData["Title"] = "Profile";
            return View();
        }
       
    }
}
