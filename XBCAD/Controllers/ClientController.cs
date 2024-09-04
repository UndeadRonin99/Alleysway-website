using Microsoft.AspNetCore.Mvc;

namespace XBCAD.Controllers
{
    public class ClientController : Controller
    {
        public IActionResult Dashboard()
        {
            ViewData["Title"] = "Client Dashboard";
            return View();
        }

        public IActionResult Profile()
        {
            ViewData["Title"] = "Profile";
            return View();
        }
       
    }
}
