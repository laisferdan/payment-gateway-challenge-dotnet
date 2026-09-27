using System.Reflection;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Http;

namespace PaymentGateway.Api.Tests.Unit.Http;

public class PaymentResultMapperTests
{
    private const string TraceId = "test-trace-id";

    [Fact]
    public void ToUnreadableBodyResult_ForAnotherAction_ReturnsValidationProblemWithoutPaymentStatus()
    {
        // Arrange
        PaymentResultMapper mapper = CreateMapper();
        ActionContext context = CreateContext(typeof(object).GetMethod(nameof(ToString))!);
        context.ModelState.AddModelError("$.amount", "The amount field is required.");

        // Act
        IActionResult result = mapper.ToUnreadableBodyResult(context);

        // Assert
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ValidationProblemDetails problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.IsNotType<PaymentRejectedProblemDetails>(problem);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public void ToUnreadableBodyResult_ForProcessPaymentAction_ReturnsPaymentRejected()
    {
        // Arrange
        PaymentResultMapper mapper = CreateMapper();
        ActionContext context = CreateContext(typeof(PaymentsController).GetMethod(nameof(PaymentsController.ProcessPaymentAsync))!);
        context.ModelState.AddModelError("$.amount", "The amount field is required.");

        // Act
        IActionResult result = mapper.ToUnreadableBodyResult(context);

        // Assert
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        PaymentRejectedProblemDetails problem = Assert.IsType<PaymentRejectedProblemDetails>(objectResult.Value);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    private static PaymentResultMapper CreateMapper()
    {
        ProblemDetailsOptions options = new()
        {
            CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = TraceId,
        };
        return new PaymentResultMapper(Options.Create(options), NullLogger<PaymentResultMapper>.Instance);
    }

    private static ActionContext CreateContext(MethodInfo method)
    {
        ControllerActionDescriptor descriptor = new() { MethodInfo = method };
        return new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
    }
}