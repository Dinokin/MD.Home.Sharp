using System;
using System.Globalization;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MD.Home.Server.Others
{
    public class HeaderInjector : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers.Add("timing-allow-origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("Server", $"MD.Home.Sharp 1.0.0 {Constants.ClientBuild}");

            context.HttpContext.Items.TryGetValue("StartTime", out var startTime);

            var totalDuration = (DateTime.UtcNow - (DateTime) startTime!).TotalMilliseconds;
            
            context.HttpContext.Response.Headers.Add("X-Time-Taken", totalDuration.ToString(CultureInfo.InvariantCulture));
            context.HttpContext.Items.Add("TTFB", totalDuration);
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}