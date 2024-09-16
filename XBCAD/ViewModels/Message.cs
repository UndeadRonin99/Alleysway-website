using Google.Cloud.Firestore;
using Microsoft.AspNetCore.Mvc;

namespace XBCAD.ViewModels
{
    public class Message : Controller
    {
        public string senderId { get; set; }
        public string senderName { get; set; }
        public string receiverId { get; set; }
        public string text { get; set; }
        public Timestamp timestamp { get; set; }
    };
}
