using NotifyFlow.Contracts;

namespace NotifyFlow.Worker.Failures;

public interface IFailureClassifier
{
    FailureClassification Classify(Exception exception, EventMessage? message);
}
