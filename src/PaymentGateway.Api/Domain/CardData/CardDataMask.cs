namespace PaymentGateway.Api.Domain.CardData;

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

        if (cardNumber.Length <= LastFourLength)
        {
            return new string(MaskCharacter, cardNumber.Length);
        }

        return new string(MaskCharacter, cardNumber.Length - LastFourLength)
            + cardNumber[^LastFourLength..];
    }
}