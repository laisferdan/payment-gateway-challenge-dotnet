using System.Collections;
using System.Text;

using Microsoft.Extensions.Logging.Testing;

namespace PaymentGateway.Api.Tests.Integration.Fixtures;

/// <summary>
/// Everything a log sink could print for one entry – message, structured state, scopes and
/// exception – so card-data assertions cover all of it, not only the message.
/// </summary>
public static class LogText
{
    // Random hex identifiers added by activity tracking. They are left out of the searchable text:
    // a short value such as a CVV can occur in them by chance, which would make the checks flaky.
    private static readonly HashSet<string> TraceIdentifierKeys = ["TraceId", "SpanId", "ParentId"];

    public static string Of(FakeLogRecord record)
    {
        StringBuilder text = new();
        text.AppendLine(record.Message);
        AppendState(text, record.State);
        foreach (object? scope in record.Scopes)
        {
            AppendState(text, scope);
        }

        text.AppendLine(record.Exception?.ToString());
        return text.ToString();
    }

    /// <summary>The TraceId the logging scope attached to the entry, if any.</summary>
    public static string? TraceId(FakeLogRecord record)
    {
        return record.Scopes
            .OfType<IEnumerable<KeyValuePair<string, object?>>>()
            .SelectMany(scope => scope)
            .Where(pair => pair.Key == "TraceId")
            .Select(pair => pair.Value?.ToString())
            .FirstOrDefault();
    }

    private static void AppendState(StringBuilder text, object? state)
    {
        if (state is IEnumerable<KeyValuePair<string, object?>> pairs)
        {
            foreach (KeyValuePair<string, object?> pair in pairs.Where(pair => !TraceIdentifierKeys.Contains(pair.Key)))
            {
                text.AppendLine($"{pair.Key}={pair.Value}");
            }

            return;
        }

        text.AppendLine(state?.ToString());
        if (state is IEnumerable items and not string)
        {
            foreach (object? item in items)
            {
                text.AppendLine(item?.ToString());
            }
        }
    }
}