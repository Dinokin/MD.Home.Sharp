using Microsoft.AspNetCore.Mvc.Filters;
using Serilog;

namespace MD.Home.Server.Filters
{
    public class ExceptionFilter : IExceptionFilter
    {
        private readonly ILogger _logger;

        public ExceptionFilter(ILogger logger) => _logger = logger;

        public void OnException(ExceptionContext context)
        {
            if (context.ExceptionHandled)
                _logger.Error($"Unhandled exception type {context.Exception.GetType()} triggered on {context.HttpContext.Request.Path}");
        }
    }
}