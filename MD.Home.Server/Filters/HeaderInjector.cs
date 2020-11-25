using System.Diagnostics.CodeAnalysis;
using MD.Home.Server.Others;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MD.Home.Server.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class HeaderInjector : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "*");
            context.HttpContext.Response.Headers.Add("Timing-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.HttpContext.Response.Headers.Add("Server", $"MD.Home.Sharp 1.0.0 {Constants.ClientBuild}");
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}