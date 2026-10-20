using System.Text.Json;
using Microsoft.Extensions.Logging;
using NotifyFlow.Contracts;

namespace NotifyFlow.Worker.Failures;

public sealed class FailureClassifier : IFailureClassifier
{
    private readonly ILogger<FailureClassifier> _logger;

    public FailureClassifier(ILogger<FailureClassifier> logger)
    {
        _logger = logger;
    }

    public FailureClassification Classify(Exception exception, EventMessage? message)
    {
        switch (exception)
        {
            case TimeoutException:
            case HttpRequestException:
            case System.Net.Sockets.SocketException:
            case IOException:
                return FailureClassification.Transient;

            // InvalidOperationException é o tipo que os handlers lançam para payload inválido (ex.: UserRegisteredHandler).
            case JsonException:
            case InvalidOperationException:
            case ArgumentException:
                return FailureClassification.InvalidMessage;

            case NotSupportedException:
                return FailureClassification.Permanent;

            default:
                _logger.LogWarning(
                    exception,
                    "Tipo de exceção fora do catálogo conhecido ({ExceptionType}) — classificando como Permanent por padrão conservador. EventId: {EventId}",
                    exception.GetType().FullName, message?.EventId);
                return FailureClassification.Unknown;
        }
    }
}
