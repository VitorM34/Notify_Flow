using NotifyFlow.Api.Validation;

namespace NotifyFlow.Api.Features.Events.ConfirmOrder;

public sealed record ConfirmOrderRequest(Guid OrderId, Guid UserId, string Email, decimal Total) : IValidatableRequest
{
    public IEnumerable<string> Validate()
    {
        if (OrderId == Guid.Empty)
        {
            yield return "OrderId não pode ser vazio.";
        }

        if (UserId == Guid.Empty)
        {
            yield return "UserId não pode ser vazio.";
        }

        if (string.IsNullOrWhiteSpace(Email))
        {
            yield return "Email é obrigatório.";
        }

        if (Total <= 0)
        {
            yield return "Total deve ser maior que zero.";
        }
    }
}
