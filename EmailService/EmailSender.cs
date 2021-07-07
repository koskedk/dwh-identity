using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using MailKit.Net.Smtp;
using Microsoft.AspNetCore.Http;
using MimeKit;
using RestSharp;
using RestSharp.Authenticators;
using Serilog;

namespace EmailService
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailConfiguration _emailConfig;

        public EmailSender(EmailConfiguration emailConfig)
        {
            _emailConfig = emailConfig;
        }

        private void Send(Message mailMessage)
        {
            try
            {
                RestClient restClient = new RestClient();
                restClient.BaseUrl = new Uri(this._emailConfig.MailGunBaseUrl);
                restClient.Authenticator = new HttpBasicAuthenticator("api", this._emailConfig.MailGunApiKey);
                RestRequest restRequest = new RestRequest();
                restRequest.AddParameter("domain", this._emailConfig.MailGunDomain, ParameterType.UrlSegment);
                restRequest.Resource = "{domain}/messages";
                restRequest.AddParameter("from", string.Concat("National Data Warehouse <", this._emailConfig.From, ">"));
                restRequest.AddParameter("to", mailMessage.To);
                restRequest.AddParameter("subject", mailMessage.Subject);
                restRequest.AddParameter("html", mailMessage.Content);
                if (Enumerable.Any<IFormFile>(mailMessage.Attachments))
                {
                    foreach (IFormFile attachment in mailMessage.Attachments)
                    {
                        restRequest.AddFile("attachment", Path.Combine("files", attachment.FileName), null);
                    }
                }
                restRequest.Method = Method.POST;
                restClient.Execute(restRequest);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message);
                throw ex;
            }
        }


        private async Task SendAsync(Message mailMessage)
        {
            try
            {
                RestClient restClient = new RestClient();
                restClient.BaseUrl = new Uri(this._emailConfig.MailGunBaseUrl);
                restClient.Authenticator = new HttpBasicAuthenticator("api", this._emailConfig.MailGunApiKey);
                RestRequest restRequest = new RestRequest();
                restRequest.AddParameter("domain", this._emailConfig.MailGunDomain, ParameterType.UrlSegment);
                restRequest.Resource = "{domain}/messages";
                restRequest.AddParameter("from", string.Concat("National Data Warehouse <", this._emailConfig.From, ">"));
                restRequest.AddParameter("to", mailMessage.To);
                restRequest.AddParameter("subject", mailMessage.Subject);
                restRequest.AddParameter("html", mailMessage.Content);
                if (mailMessage.Attachments != null && Enumerable.Any<IFormFile>(mailMessage.Attachments))
                {
                    foreach (IFormFile attachment in mailMessage.Attachments)
                    {
                        restRequest.AddFile("attachment", Path.Combine("files", attachment.FileName), null);
                    }
                }
                restRequest.Method = Method.POST;
                await restClient.ExecuteAsync(restRequest);
            }
            catch (Exception exception)
            {
                Log.Error(exception.Message);
                throw exception;
            }
        }

        public void SendEmail(Message message)
        {
            this.Send(message);
        }

        public async Task SendEmailAsync(Message message)
        {
            await this.SendAsync(message);
        }
    }
}
