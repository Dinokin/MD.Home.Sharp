using System;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
using MD.Home.Sharp.Cache;
using MD.Home.Sharp.Others;
using Microsoft.AspNetCore.Mvc.Filters;

namespace MD.Home.Sharp.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal sealed class HeaderInjector : IResultFilter
    {
        public void OnResultExecuting(ResultExecutingContext context)
        {
            context.HttpContext.Response.Headers.Add("Access-Control-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("Access-Control-Expose-Headers", "*");
            context.HttpContext.Response.Headers.Add("Timing-Allow-Origin", "https://mangadex.org");
            context.HttpContext.Response.Headers.Add("X-Content-Type-Options", "nosniff");
            context.HttpContext.Response.Headers.Add("Server", $"MD.Home.Sharp 1.0.0 {Constants.ClientBuild}");
            context.HttpContext.Response.Headers.Add("Date", DateTimeOffset.UtcNow.ToString("O"));
            
            var timeTaken = (DateTime.UtcNow - (DateTime) context.HttpContext.Items["StartTime"]!);
            context.HttpContext.Items.Add("TimeTaken", timeTaken.TotalMilliseconds);
            
            switch (context.HttpContext.Response.Headers["X-Cache"].FirstOrDefault())
            {
                case "HIT":
                    CacheStatistics.IncrementHit();
                    CacheStatistics.IncrementHitTtfb(timeTaken);
                    break;
                case "MISS":
                    CacheStatistics.IncrementMiss();
                    CacheStatistics.IncrementMissTtfb(timeTaken);
                    break;
            }

            context.HttpContext.Response.Headers.Add("X-Time-Taken", timeTaken.TotalMilliseconds.ToString(CultureInfo.InvariantCulture));
        }

        public void OnResultExecuted(ResultExecutedContext context) { }
    }
}