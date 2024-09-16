using Microsoft.AspNetCore.Mvc;

namespace XBCAD.ViewModels
{
    public class BookedSession : Controller
    {
        public string trainerID { get; set; }
        public string clientID { get; set; }
        public bool payed {  get; set; }
        public int totalAmount { get; set; }
        public DateTime DateTime { get; set; }
    }
}
