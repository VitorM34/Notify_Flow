using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using RabbitMQ.Client.Exceptions;

namespace NotifyFlow.Api.ErrorHandling;

public sealed class GlobalExceptionHandler : IExceptionHandler
{
    private readonly ILogger<GlobalExceptionHandler> _logger;

    public GlobalExceptionHandler(ILogger<GlobalExceptionHandler> logger)
    {
        _logger = logger;
    }

    public async ValueTask<bool> TryHandleAsync(
        HttpContext httpContext,
        Exception exception,
        CancellationToken cancellationToken)
    {
        if (exception is BrokerUnreachableException or AlreadyClosedException)
        {
            _logger.LogError(exception, "Falha ao publicar evento: RabbitMQ indisponível");

            httpContext.Response.StatusCode = StatusCodes.Status503ServiceUnavailable;

            await httpContext.Response.WriteAsJsonAsync(new ProblemDetails
            {
                Status = StatusCodes.Status503ServiceUnavailable,
                Title = "Serviço de mensageria indisponível",
                Detail = "Não foi possível publicar o evento no momento. Tente novamente em instantes.",
                Type = "https://tools.ietf.org/html/rfc9110#section-15.6.4"
            }, cancellationToken);

            return true;
        }

        _logger.LogError(exception, "Exceção não tratada ao processar {Path}", httpContext.Request.Path);

        return false;
    }
}
