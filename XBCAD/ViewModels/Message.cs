using Google.Cloud.Firestore;

namespace XBCAD.ViewModels
{
    public class Message
    {
        public string senderId { get; set; }
        public string senderName { get; set; }
        public string receiverId { get; set; }
        public string text { get; set; }
        public Timestamp timestamp { get; set; }
    }
}
