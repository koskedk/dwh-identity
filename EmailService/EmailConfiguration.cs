using System;
using System.Collections.Generic;
using System.Text;

namespace EmailService
{
    public class EmailConfiguration
    {
        public string From { get; set; }

        public string MailGunApiKey
        {
            get;
            set;
        }

        public string MailGunBaseUrl
        {
            get;
            set;
        }

        public string MailGunDomain
        {
            get;
            set;
        }

        public EmailConfiguration()
        {
        }
    }
}
