using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography.X509Certificates;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace Dwh.IS4Host
{
    public static class SigninKeyExtension
    {
        public static void AddCertificateFromFile(this IIdentityServerBuilder builder, IConfiguration options,
            IWebHostEnvironment environment)
        {
            var keyFilePath = Path.Combine(environment.ContentRootPath,
                options.GetSection("SigninKeyCredentials:KeyFilePath").Value);
            var keyFilePassword = options.GetSection("SigninKeyCredentials:KeyFilePassword").Value;

            if (!File.Exists(keyFilePath))
            {
                Log.Error($"File not found {keyFilePath}");
                return;
            }

            builder.AddSigningCredential(new X509Certificate2(keyFilePath, keyFilePassword));
        }
    }
}
