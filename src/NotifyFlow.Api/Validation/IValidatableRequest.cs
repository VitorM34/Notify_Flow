namespace NotifyFlow.Api.Validation;

public interface IValidatableRequest
{
    IEnumerable<string> Validate();
}
