using NotifyFlow.Api.Validation;

namespace NotifyFlow.Api.Features.Events.RegisterUser;

public sealed record RegisterUserRequest(Guid UserId, string Email, string Name) : IValidatableRequest
{
    public IEnumerable<string> Validate()
    {
        if (UserId == Guid.Empty)
        {
            yield return "UserId não pode ser vazio.";
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return "Email é obrigatório.";
        }

        if (string.IsNullOrWhiteSpace(Name))
        {
            yield return "Name é obrigatório.";
        }
    }
}
