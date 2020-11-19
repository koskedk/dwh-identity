using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Dwh.IS4Host
{
    public static class SigninKeyExtension
    {
        public static void AddCertificateFromFile(this IIdentityServerBuilder builder, IConfiguration options)
        {
            var keyFilePath = options.GetSection("SigninKeyCredentials:KeyFilePath").Value;
            var keyFilePassword = options.GetSection("SigninKeyCredentials:KeyFilePassword").Value;

            if (File.Exists(keyFilePath))
            {
                //logger.LogDebug($"SigninCredentialExtension adding key from file {keyFilePath}");

                // You can simply add this line in the Startup.cs if you don't want an extension. 
                // This is neater though ;)
                builder.AddSigningCredential(new X509Certificate2(keyFilePath, keyFilePassword));
            }
        }
    }
}
