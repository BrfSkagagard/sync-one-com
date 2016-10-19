using System.Collections.Generic;

namespace OneCom
{
    public class EmailInfo
    {
        public string Email { get; set; }
        public string EditUrl { get; set; }
        public List<string> ForwardAddresses { get; set; }

        public EmailInfo()
        {
            ForwardAddresses = new List<string>();
        }

        public override string ToString()
        {
            return Email + " ( " + string.Join(",", ForwardAddresses) + " )";
        }
    }
}