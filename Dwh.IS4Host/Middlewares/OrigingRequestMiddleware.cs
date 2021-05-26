using System.Globalization;
using System.Threading.Tasks;
using IdentityServer4.Extensions;
using Microsoft.AspNetCore.Http;
using Serilog;

namespace Dwh.IS4Host.Middlewares
{
    public class OrigingRequestMiddleware
    {
        private readonly RequestDelegate _next;

        public OrigingRequestMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            context.SetIdentityServerOrigin("https://dwh.nascop.org:7010");

            // Call the next delegate/middleware in the pipeline
            await _next(context);
        }
    }
}
