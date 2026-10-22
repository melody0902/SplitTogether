using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace SplitTogether.Services;

public class CsrfFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter, IOrderedFilter
{
    public int Order => 1000;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (
            HttpMethods.IsGet(context.HttpContext.Request.Method)
            || HttpMethods.IsHead(context.HttpContext.Request.Method)
            || HttpMethods.IsOptions(context.HttpContext.Request.Method)
        )
            return;
        try
        {
            await antiforgery.ValidateRequestAsync(context.HttpContext);
        }
        catch (AntiforgeryValidationException)
        {
            context.Result = new BadRequestObjectResult(
                new ProblemDetails
                {
                    Status = 400,
                    Title = "驗證已過期，請重新整理後再試",
                    Extensions = { ["code"] = "csrf_invalid" },
                }
            );
        }
    }
}
