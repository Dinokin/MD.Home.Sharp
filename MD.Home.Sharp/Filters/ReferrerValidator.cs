﻿using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MD.Home.Sharp.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace MD.Home.Sharp.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    internal sealed class ReferrerValidator : IActionFilter
    {
        [SuppressMessage("ReSharper", "InvertIf")]
        [SuppressMessage("ReSharper", "RedundantJumpStatement")]
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var path = context.HttpContext.Request.Path.Value.RemoveToken();
            string[] allowedReferrers = {"https://mangadex.org", "https://mangadex.network", string.Empty};

            if (context.HttpContext.Request.Headers.TryGetValue("Referer", out var referer) && !referer.Any(str => allowedReferrers.Any(str.Contains)))
            {
                Log.Logger.Warning($"Request for {path} rejected due to non-allowed referrer ${string.Join(',', context.HttpContext.Request.Headers["Referer"])}");
                
                context.Result = new StatusCodeResult(403);
                
                return;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}