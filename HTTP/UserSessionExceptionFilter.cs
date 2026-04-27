using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using TechnoartSDK.Models;

namespace TechnoartSDK.HTTP;

/// <summary>
/// Global exception filter that maps <see cref="UserSessionException"/> to HTTP 401 Unauthorized.
/// Register globally: <c>options.Filters.Add&lt;UserSessionExceptionFilter&gt;();</c>
/// </summary>
public class UserSessionExceptionFilter : IExceptionFilter
{
    /// <inheritdoc/>
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not UserSessionException)
        {
            return;
        }

        context.Result = new ObjectResult(new ApiErrorResponse { Message = context.Exception.Message })
        {
            StatusCode = 401,
        };

        context.ExceptionHandled = true;
    }
}
