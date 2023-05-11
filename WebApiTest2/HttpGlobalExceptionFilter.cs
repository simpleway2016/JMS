
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;

public class HttpGlobalExceptionFilter : IExceptionFilter
{
    private readonly ILogger<HttpGlobalExceptionFilter> logger;

    public HttpGlobalExceptionFilter(ILogger<HttpGlobalExceptionFilter> logger)
    {
        this.logger = logger;
    }

    public void OnException(ExceptionContext context)
    {
        
    }

    private class JsonErrorResponse
    {
        public string[] Messages { get; set; }

        public object DeveloperMessage { get; set; }
    }
}
