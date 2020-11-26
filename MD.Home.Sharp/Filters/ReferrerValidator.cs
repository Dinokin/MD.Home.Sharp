using System.Diagnostics.CodeAnalysis;
using System.Linq;
using MD.Home.Sharp.Extensions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace MD.Home.Sharp.Filters
{
    [SuppressMessage("ReSharper", "ClassNeverInstantiated.Global")]
    public class ReferrerValidator : IActionFilter
    {
        private readonly ILogger _logger;

        public ReferrerValidator(ILogger logger) => _logger = logger;
        
        [SuppressMessage("ReSharper", "InvertIf")]
        [SuppressMessage("ReSharper", "RedundantJumpStatement")]
        public void OnActionExecuting(ActionExecutingContext context)
        {
            var path = context.HttpContext.Request.Path.Value.GetFilteredPath();
            string[] allowedReferrers = {"https://mangadex.org", "https://mangadex.network", string.Empty};

            if (context.HttpContext.Request.Headers.TryGetValue("Referer", out var referer) && !referer.Any(str => allowedReferrers.Any(str.Contains)))
            {
                _logger.Information($"Request for {path} rejected due to non-allowed referrer ${string.Join(',', context.HttpContext.Request.Headers["Referer"])}");
                
                context.Result = new StatusCodeResult(403);
                
                return;
            }
        }

        public void OnActionExecuted(ActionExecutedContext context) { }
    }
}