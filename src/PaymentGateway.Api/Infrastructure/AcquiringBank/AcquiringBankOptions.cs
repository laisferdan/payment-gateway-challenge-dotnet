using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

/// <summary>
/// Where the acquiring bank lives and how long to wait for it. Validated at startup so a
/// misconfigured gateway fails fast instead of on the first payment.
/// </summary>
public sealed class AcquiringBankOptions
{
    public const string SectionName = "AcquiringBank";

    public const int MinTimeoutSeconds = 1;
    public const int MaxTimeoutSeconds = 60;
    public const int DefaultTimeoutSeconds = 10;

    [Required]
    [Url]
    public string BaseUrl { get; set; } = string.Empty;

    [Range(MinTimeoutSeconds, MaxTimeoutSeconds)]
    public int TimeoutSeconds { get; set; } = DefaultTimeoutSeconds;
}