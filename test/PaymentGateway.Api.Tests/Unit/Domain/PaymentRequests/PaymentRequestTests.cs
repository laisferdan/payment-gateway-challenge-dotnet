using PaymentGateway.Api.Domain.PaymentRequests;

namespace PaymentGateway.Api.Tests.Unit.Domain.PaymentRequests;

public class PaymentRequestTests
{
    private const string ValidCardNumber = "2222405343248877";
    private const int ValidExpiryMonth = 12;
    private const int ValidExpiryYear = 2030;
    private const string ValidCurrency = "GBP";
    private const int ValidAmount = 1050;
    private const string ValidCvv = "123";

    private static readonly DateOnly Today = new(2026, 9, 26);

    [Fact]
    public void Create_WhenAllFieldsAreValid_ReturnsRequest()
    {
        // Act
        CreatePaymentRequestResult result = PaymentRequest.Create(
            ValidCardNumber, ValidExpiryMonth, ValidExpiryYear, ValidCurrency, ValidAmount, ValidCvv, Today);

        // Assert
        PaymentRequest request = RequestOf(result);
        Assert.Equal(ValidCardNumber, request.CardNumber);
        Assert.Equal(ValidExpiryMonth, request.ExpiryMonth);
        Assert.Equal(ValidExpiryYear, request.ExpiryYear);
        Assert.Equal(ValidCurrency, request.Currency);
        Assert.Equal(ValidAmount, request.Amount);
        Assert.Equal(ValidCvv, request.Cvv);
        Assert.Equal("8877", request.CardNumberLastFour);
    }

    [Fact]
    public void Create_WhenCardNumberHasLeadingZeroLastFour_KeepsZeros()
    {
        // Arrange
        const string cardNumber = "2222405343240012";

        // Act
        CreatePaymentRequestResult result = PaymentRequest.Create(
            cardNumber, ValidExpiryMonth, ValidExpiryYear, ValidCurrency, ValidAmount, ValidCvv, Today);

        // Assert
        Assert.Equal("0012", RequestOf(result).CardNumberLastFour);
    }

    [Theory]
    [InlineData("22224053432488")]
    [InlineData("2222405343248877123")]
    public void Create_WhenCardNumberIsWithinBounds_Accepts(string cardNumber)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cardNumber: cardNumber);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Theory]
    [InlineData("2222405343248")]
    [InlineData("22224053432488771234")]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("2222 4053 4324 8877")]
    [InlineData("2222-4053-4324-8877")]
    [InlineData("22224053432488a7")]
    [InlineData("٢٢٢٢٤٠٥٣٤٣٢٤٨٨٧٧")]
    public void Create_WhenCardNumberIsInvalid_RejectsCardNumberWithoutEchoingIt(string? cardNumber)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cardNumber: cardNumber);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.CardNumber), cardNumber);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(12)]
    public void Create_WhenExpiryMonthIsWithinBounds_Accepts(int expiryMonth)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryMonth: expiryMonth);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(13)]
    [InlineData(null)]
    public void Create_WhenExpiryMonthIsInvalid_RejectsExpiryMonth(int? expiryMonth)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryMonth: expiryMonth);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.ExpiryMonth), null);
    }

    [Theory]
    [InlineData(9, 2026)]
    [InlineData(12, 2046)]
    public void Create_WhenExpiryIsCurrentMonthOrLater_Accepts(int expiryMonth, int expiryYear)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryMonth: expiryMonth, expiryYear: expiryYear);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Theory]
    [InlineData(2025)]
    [InlineData(27)]
    [InlineData(2047)]
    [InlineData(10000)]
    [InlineData(null)]
    public void Create_WhenExpiryYearIsInvalid_RejectsExpiryYear(int? expiryYear)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryYear: expiryYear);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.ExpiryYear), null);
    }

    [Fact]
    public void Create_WhenExpiryIsLastMonth_RejectsExpiryMonthAndYear()
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryMonth: 8, expiryYear: 2026);

        // Assert
        Assert.Equal(
            [nameof(PaymentRequest.ExpiryMonth), nameof(PaymentRequest.ExpiryYear)],
            ErrorsOf(result).Select(error => error.Field));
        Assert.All(ErrorsOf(result), error => Assert.Equal(PaymentRequest.Messages.Expired, error.Message));
    }

    [Fact]
    public void Create_WhenTodayIsNewYearsEveAndExpiryIsJanuaryNextYear_Accepts()
    {
        // Arrange
        DateOnly newYearsEve = new(2026, 12, 31);

        // Act
        CreatePaymentRequestResult result = PaymentRequest.Create(
            ValidCardNumber, expiryMonth: 1, expiryYear: 2027, ValidCurrency, ValidAmount, ValidCvv, newYearsEve);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Fact]
    public void Create_WhenTodayIsNewYearsEveAndExpiryIsDecemberLastYear_RejectsExpiryYear()
    {
        // Arrange
        DateOnly newYearsEve = new(2026, 12, 31);

        // Act
        CreatePaymentRequestResult result = PaymentRequest.Create(
            ValidCardNumber, expiryMonth: 12, expiryYear: 2025, ValidCurrency, ValidAmount, ValidCvv, newYearsEve);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.ExpiryYear), null);
    }

    [Fact]
    public void Create_WhenMonthAndYearAreBothInvalid_DoesNotAddCombinationError()
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(expiryMonth: 13, expiryYear: 2020);

        // Assert
        Assert.Equal(
            [nameof(PaymentRequest.ExpiryMonth), nameof(PaymentRequest.ExpiryYear)],
            ErrorsOf(result).Select(error => error.Field));
    }

    [Theory]
    [InlineData("GBP")]
    [InlineData("EUR")]
    [InlineData("USD")]
    public void Create_WhenCurrencyIsSupported_Accepts(string currency)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(currency: currency);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Theory]
    [InlineData("gbp")]
    [InlineData("Gbp")]
    [InlineData("GBPX")]
    [InlineData("GB")]
    [InlineData("JPY")]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WhenCurrencyIsInvalid_RejectsCurrencyListingSupportedCodes(string? currency)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(currency: currency);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.Currency), null);
        Assert.Equal("Currency must be one of: GBP, EUR, USD.", ErrorsOf(result)[0].Message);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(int.MaxValue)]
    public void Create_WhenAmountIsPositive_Accepts(int amount)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(amount: amount);

        // Assert
        Assert.IsType<CreatePaymentRequestResult.Valid>(result);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(0)]
    [InlineData(null)]
    public void Create_WhenAmountIsInvalid_RejectsAmount(int? amount)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(amount: amount);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.Amount), null);
    }

    [Theory]
    [InlineData("123")]
    [InlineData("0123")]
    public void Create_WhenCvvIsWithinBounds_AcceptsAndKeepsLeadingZeros(string cvv)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cvv: cvv);

        // Assert
        Assert.Equal(cvv, RequestOf(result).Cvv);
    }

    [Theory]
    [InlineData("12")]
    [InlineData("12345")]
    [InlineData("12a")]
    [InlineData(null)]
    [InlineData("")]
    public void Create_WhenCvvIsInvalid_RejectsCvvWithoutEchoingIt(string? cvv)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cvv: cvv);

        // Assert
        AssertSingleErrorOn(result, nameof(PaymentRequest.Cvv), cvv);
    }

    [Fact]
    public void Create_WhenSeveralFieldsAreInvalid_ReturnsEveryError()
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cardNumber: "1234", currency: "gbp", amount: 0);

        // Assert
        Assert.Equal(
            [nameof(PaymentRequest.CardNumber), nameof(PaymentRequest.Currency), nameof(PaymentRequest.Amount)],
            ErrorsOf(result).Select(error => error.Field));
    }

    [Theory]
    [InlineData(ValidCardNumber + " ", ValidCurrency, ValidCvv, nameof(PaymentRequest.CardNumber))]
    [InlineData(ValidCardNumber, " " + ValidCurrency, ValidCvv, nameof(PaymentRequest.Currency))]
    [InlineData(ValidCardNumber, ValidCurrency, " " + ValidCvv, nameof(PaymentRequest.Cvv))]
    public void Create_WhenValueNeedsTrimming_IsRejected(string cardNumber, string currency, string cvv, string field)
    {
        // Act
        CreatePaymentRequestResult result = CreateWith(cardNumber: cardNumber, currency: currency, cvv: cvv);

        // Assert
        Assert.Equal(field, Assert.Single(ErrorsOf(result)).Field);
    }

    [Fact]
    public void ToString_Always_MasksCardNumberAndOmitsCvv()
    {
        // Arrange
        PaymentRequest request = RequestOf(CreateWith(cvv: "987"));

        // Act
        string? text = request.ToString();

        // Assert
        Assert.Contains("************8877", text);
        Assert.Contains("12/2030", text);
        Assert.Contains("GBP", text);
        Assert.Contains("1050", text);
        Assert.DoesNotContain(ValidCardNumber, text);
        Assert.DoesNotContain("987", text);
    }

    private static CreatePaymentRequestResult CreateWith(
        string? cardNumber = ValidCardNumber,
        int? expiryMonth = ValidExpiryMonth,
        int? expiryYear = ValidExpiryYear,
        string? currency = ValidCurrency,
        int? amount = ValidAmount,
        string? cvv = ValidCvv)
    {
        return PaymentRequest.Create(cardNumber, expiryMonth, expiryYear, currency, amount, cvv, Today);
    }

    private static PaymentRequest RequestOf(CreatePaymentRequestResult result)
    {
        return Assert.IsType<CreatePaymentRequestResult.Valid>(result).Request;
    }

    private static IReadOnlyList<ValidationError> ErrorsOf(CreatePaymentRequestResult result)
    {
        return Assert.IsType<CreatePaymentRequestResult.Invalid>(result).Errors;
    }

    private static void AssertSingleErrorOn(CreatePaymentRequestResult result, string field, string? submitted)
    {
        ValidationError error = Assert.Single(ErrorsOf(result));
        Assert.Equal(field, error.Field);
        Assert.False(string.IsNullOrWhiteSpace(error.Message));
        Assert.True(string.IsNullOrEmpty(submitted) || !error.Message.Contains(submitted), "The message echoes the submitted value.");
    }
}