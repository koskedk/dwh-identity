using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.AspNetCore.Http;
using MimeKit;

namespace EmailService
{
    public class Message
    {
        public IFormFileCollection Attachments
        {
            get;
            set;
        }

        public string Content
        {
            get;
            set;
        }

        public string Subject
        {
            get;
            set;
        }

        public string To
        {
            get;
            set;
        }

        public Message(string to, string subject, string content, IFormFileCollection attachments)
        {
            this.To = to;
            this.Subject = subject;
            this.Content = content;
            this.Attachments = attachments;
        }
    }
}
