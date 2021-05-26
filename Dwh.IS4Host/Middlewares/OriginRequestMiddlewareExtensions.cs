using Microsoft.AspNetCore.Builder;

namespace Dwh.IS4Host.Middlewares
{
    public static class OriginRequestMiddlewareExtensions
    {
        public static IApplicationBuilder UseRequestCulture(
            this IApplicationBuilder builder)
        {
            return builder.UseMiddleware<OrigingRequestMiddleware>();
        }
    }
}
