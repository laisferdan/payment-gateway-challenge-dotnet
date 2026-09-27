using System.Collections;
using System.Text;

using Microsoft.Extensions.Logging.Testing;

namespace PaymentGateway.Api.Tests.Integration.Fixtures;

public static class LogText
{
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