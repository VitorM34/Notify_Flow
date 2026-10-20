namespace NotifyFlow.Worker.Failures;

public enum FailureClassification
{
    Transient,
    Permanent,
    InvalidMessage,
    Unknown
}
