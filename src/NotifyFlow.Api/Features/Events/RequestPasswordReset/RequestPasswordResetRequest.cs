using NotifyFlow.Api.Validation;

namespace NotifyFlow.Api.Features.Events.RequestPasswordReset;

public sealed record RequestPasswordResetRequest(Guid UserId, string Email, string ResetToken) : IValidatableRequest
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

        if (string.IsNullOrWhiteSpace(ResetToken))
        {
            yield return "ResetToken é obrigatório.";
        }
    }
}
