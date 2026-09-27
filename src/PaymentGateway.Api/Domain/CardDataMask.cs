namespace PaymentGateway.Api.Domain;

/// <summary>
/// The only way card data is ever printed: the card number masked down to its last four digits
/// and the CVV not at all. Used by every <c>ToString()</c> of a type carrying card data.
/// </summary>
public static class CardDataMask
{
    private const char MaskCharacter = '*';

    public static string MaskCardNumber(string? cardNumber)
    {
        if (cardNumber is null)
        {
            return string.Empty;
        }

        // Short (invalid) values are masked entirely: they could be any part of a card number.
        if (cardNumber.Length <= PaymentRequest.LastFourLength)
        {
            return new string(MaskCharacter, cardNumber.Length);
        }

        return new string(MaskCharacter, cardNumber.Length - PaymentRequest.LastFourLength)
            + cardNumber[^PaymentRequest.LastFourLength..];
    }
}