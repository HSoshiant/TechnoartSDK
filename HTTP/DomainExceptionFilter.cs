using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace TechnoartSDK.HTTP;

/// <summary>
/// Maps common domain exceptions to proper HTTP responses using ProblemDetails:
/// <list type="bullet">
///   <item><see cref="UnauthorizedAccessException"/> → 403 Forbidden</item>
///   <item><see cref="ArgumentNullException"/> → 404 Not Found</item>
///   <item><see cref="ArgumentException"/> → 400 Bad Request</item>
/// </list>
/// Register globally: <c>options.Filters.Add&lt;DomainExceptionFilter&gt;();</c>
/// </summary>
public class DomainExceptionFilter : IExceptionFilter
{
    #region Methods

    public void OnException(ExceptionContext context)
    {
        switch (context.Exception)
        {
            case UnauthorizedAccessException ex:
                context.Result = new ObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status403Forbidden,
                    Title = "Forbidden",
                    Detail = ex.Message,
                })
                { StatusCode = StatusCodes.Status403Forbidden };
                context.ExceptionHandled = true;
                break;

            case ArgumentNullException ex:
                context.Result = new NotFoundObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status404NotFound,
                    Title = "Not Found",
                    Detail = ex.Message,
                });
                context.ExceptionHandled = true;
                break;

            case ArgumentException ex:
                context.Result = new BadRequestObjectResult(new ProblemDetails
                {
                    Status = StatusCodes.Status400BadRequest,
                    Title = "Bad Request",
                    Detail = ex.Message,
                });
                context.ExceptionHandled = true;
                break;
        }
    }

    #endregion Methods
}
