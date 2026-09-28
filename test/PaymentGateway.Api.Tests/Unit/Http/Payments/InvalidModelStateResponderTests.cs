using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using PaymentGateway.Api.Http.Payments;

namespace PaymentGateway.Api.Tests.Unit.Http.Payments;

public class InvalidModelStateResponderTests
{
    private const string TraceId = "test-trace-id";

    [Fact]
    public void Respond_ForActionWithoutDeclaredResponse_ReturnsValidationProblemWithoutPaymentStatus()
    {
        // Arrange
        InvalidModelStateResponder responder = CreateResponder();
        ActionContext context = CreateContext();
        context.ModelState.AddModelError("$.amount", "The amount field is required.");

        // Act
        IActionResult result = responder.Respond(context);

        // Assert
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ValidationProblemDetails problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public void Respond_ForPaymentRejectedAction_ReturnsPaymentRejectedWithTheFieldRule()
    {
        // Arrange
        InvalidModelStateResponder responder = CreateResponder();
        ActionContext context = CreateContext(InvalidRequestResponse.PaymentRejected);
        context.ModelState.AddModelError("$.amount", "The amount field is required.");

        // Act
        IActionResult result = responder.Respond(context);

        // Assert
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        PaymentRejectedProblemDetails problem = Assert.IsType<PaymentRejectedProblemDetails>(objectResult.Value);
        Assert.Equal(["amount"], problem.Errors.Keys);
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    [Fact]
    public void Respond_ForInvalidPaymentIdAction_ReturnsInvalidIdWithoutEchoingIt()
    {
        // Arrange
        InvalidModelStateResponder responder = CreateResponder();
        ActionContext context = CreateContext(InvalidRequestResponse.InvalidPaymentId);
        context.ModelState.AddModelError("id", "The value '4111111111111111' is not valid.");

        // Act
        IActionResult result = responder.Respond(context);

        // Assert
        ObjectResult objectResult = Assert.IsType<ObjectResult>(result);
        Assert.Equal(StatusCodes.Status400BadRequest, objectResult.StatusCode);
        ValidationProblemDetails problem = Assert.IsType<ValidationProblemDetails>(objectResult.Value);
        Assert.IsNotType<PaymentRejectedProblemDetails>(problem);
        Assert.Equal("Invalid payment id", problem.Title);
        Assert.DoesNotContain("4111111111111111", Assert.Single(problem.Errors["id"]));
        Assert.Equal(TraceId, problem.Extensions["traceId"]);
    }

    private static InvalidModelStateResponder CreateResponder()
    {
        ProblemDetailsOptions options = new()
        {
            CustomizeProblemDetails = context => context.ProblemDetails.Extensions["traceId"] = TraceId,
        };
        return new InvalidModelStateResponder(new PaymentResultMapper(Options.Create(options)), NullLogger<InvalidModelStateResponder>.Instance);
    }

    private static ActionContext CreateContext(InvalidRequestResponse? response = null)
    {
        ActionDescriptor descriptor = new()
        {
            EndpointMetadata = response is InvalidRequestResponse declared ? [new RespondsToInvalidRequestAttribute(declared)] : [],
        };
        return new ActionContext(new DefaultHttpContext(), new RouteData(), descriptor);
    }
}