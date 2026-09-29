namespace PaymentGateway.Api.Domain.CardData;

// Used by every ToString() of a type carrying card data; the CVV is never printed at all.
public static class CardDataMask
{
    public const int LastFourLength = 4;

    private const char MaskCharacter = '*';

    public static string MaskCardNumber(string? cardNumber)
    {
        if (cardNumber is null)
        {
            return string.Empty;
        }

        // Short (invalid) values are masked entirely: they could be any part of a card number.
        if (cardNumber.Length <= LastFourLength)
        {
            return new string(MaskCharacter, cardNumber.Length);
        }

        return new string(MaskCharacter, cardNumber.Length - LastFourLength)
            + cardNumber[^LastFourLength..];
    }
}