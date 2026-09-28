using PaymentGateway.Api.Domain.PaymentRequests;
using PaymentGateway.Api.Http.Payments;

namespace PaymentGateway.Api.Tests.Unit.Http.Payments;

public class PaymentFieldNamesTests
{
    [Theory]
    [InlineData(PaymentField.CardNumber, "cardNumber")]
    [InlineData(PaymentField.ExpiryMonth, "expiryMonth")]
    [InlineData(PaymentField.ExpiryYear, "expiryYear")]
    [InlineData(PaymentField.Currency, "currency")]
    [InlineData(PaymentField.Amount, "amount")]
    [InlineData(PaymentField.Cvv, "cvv")]
    public void ToJsonName_ForEachField_IsTheRequestPropertyJsonName(PaymentField field, string expected)
    {
        // Arrange

        // Act
        string name = PaymentFieldNames.ToJsonName(field);

        // Assert
        Assert.Equal(expected, name);
    }

    [Theory]
    [InlineData("expiryMonth")]
    [InlineData("ExpiryMonth")]
    public void TryParse_ForAFieldNameInAnyCase_FindsTheField(string jsonName)
    {
        // Arrange

        // Act
        bool found = PaymentFieldNames.TryParse(jsonName, out PaymentField field);

        // Assert
        Assert.True(found);
        Assert.Equal(PaymentField.ExpiryMonth, field);
    }

    [Theory]
    [InlineData("")]
    [InlineData("body")]
    public void TryParse_ForAnUnknownName_FindsNothing(string jsonName)
    {
        // Arrange

        // Act
        bool found = PaymentFieldNames.TryParse(jsonName, out _);

        // Assert
        Assert.False(found);
    }
}