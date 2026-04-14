using Microsoft.AspNetCore.SignalR;

namespace Faturamento.API.Infrastructure.Hubs;

/// <summary>
/// SignalR Hub para notificações em tempo real ao frontend.
/// Quando o consumer do RabbitMQ confirmar o fechamento da nota,
/// emite "NotaAtualizada" para todos os clientes conectados.
/// </summary>
public class NotaFiscalHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        await base.OnConnectedAsync();
    }
}
