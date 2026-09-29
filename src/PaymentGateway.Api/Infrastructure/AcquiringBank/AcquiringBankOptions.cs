using System.ComponentModel.DataAnnotations;

namespace PaymentGateway.Api.Infrastructure.AcquiringBank;

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