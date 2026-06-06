using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Beep.OilGasSim.Api.Filters;

public sealed class GameApiExceptionFilter : IExceptionFilter
{
    public void OnException(ExceptionContext context)
    {
        if (context.Exception is not InvalidOperationException ex)
        {
            return;
        }

        context.Result = new BadRequestObjectResult(new { error = ex.Message });
        context.ExceptionHandled = true;
    }
}
