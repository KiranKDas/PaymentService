using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace PaymentService.Attributes;

public class RequireUserTypeAttribute : ActionFilterAttribute
{
    private readonly string _requiredUserType;

    public RequireUserTypeAttribute(string requiredUserType = "paymentGateway")
    {
        _requiredUserType = requiredUserType;
    }

    public override void OnActionExecuting(ActionExecutingContext context)
    {
        if (!context.HttpContext.Request.Headers.TryGetValue("userType", out var extractedUserType) || 
            extractedUserType != _requiredUserType)
        {
            context.Result = new UnauthorizedObjectResult(new { Message = "Unauthorized. Invalid or missing userType header." });
            return;
        }

        base.OnActionExecuting(context);
    }
}