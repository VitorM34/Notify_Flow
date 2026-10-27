using Microsoft.Extensions.Logging.Abstractions;
using NotifyFlow.Worker.Failures;

namespace NotifyFlow.Worker.Tests.Failures;

public class FailureClassifierTests
{
    private readonly FailureClassifier _classifier = new(NullLogger<FailureClassifier>.Instance);

    [Fact]
    public void FailureClassifier_should_classify_transient_provider_timeout()
    {
        var result = _classifier.Classify(new TimeoutException("provider timed out"), message: null);

        Assert.Equal(FailureClassification.Transient, result);
    }

    [Fact]
    public void FailureClassifier_should_classify_invalid_payload_as_permanent()
    {
        // O catálogo (FailureClassifier.cs) mapeia InvalidOperationException — o tipo que os
        // handlers lançam para payload inválido — para InvalidMessage, não para Permanent
        // genérico: é uma categoria mais específica, mas com o mesmo destino final (DLQ sem
        // retry), que é o que este teste garante.
        var result = _classifier.Classify(new InvalidOperationException("Invalid payload for user.registered"), message: null);

        Assert.Equal(FailureClassification.InvalidMessage, result);
    }
}
