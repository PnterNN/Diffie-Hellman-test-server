using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SProjectServer.model
{
    public class MessageModel
    {
        public string MessageID { get; set; }
        public string SenderID { get; set; }
        public string ReceiverID { get; set; }
        public string MessageText { get; set; }
        public DateTime Time { get; set; }
    }
}
