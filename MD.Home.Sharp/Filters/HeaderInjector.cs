using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using MD.Home.Sharp.Others;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MD.Home.Sharp.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal sealed class HeaderInjector : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Items.TryGetValue("StartTime", out var startTime);
            var timeTaken = (DateTime.UtcNow - (DateTime) startTime!).TotalMilliseconds;
            context.HttpContext.Items.Add("TimeTaken", timeTaken);
            
            context.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "*");
            context.HttpContext.Response.Headers.Add("Timing-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.HttpContext.Response.Headers.Add("X-Time-Taken", timeTaken.ToString(CultureInfo.InvariantCulture));
            context.HttpContext.Response.Headers.Add("Server", $"MD.Home.Sharp 1.0.0 {Constants.ClientBuild}");
            context.HttpContext.Response.Headers.Add("Date", DateTimeOffset.UtcNow.ToString("O"));
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}