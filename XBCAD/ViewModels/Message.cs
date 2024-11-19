// In ViewModels/Message.cs
using Google.Cloud.Firestore;

namespace XBCAD.ViewModels
{
 // Represents a message exchanged between a sender and a receiver.
    public class Message
    {
         // Unique identifier for the sender of the message.
        public string senderId { get; set; }
        
       // Name of the sender of the message.
        public string senderName { get; set; }
        
         // Unique identifier for the receiver of the message.
        public string receiverId { get; set; }


         // The content of the message sent.
        public string text { get; set; }
        
     // Timestamp indicating when the message was sent, using Firestore's Timestamp.
        public Timestamp timestamp { get; set; }
    }
}
