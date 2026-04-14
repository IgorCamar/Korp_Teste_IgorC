using Faturamento.API.Application.DTOs;
using Faturamento.API.Infrastructure.Messaging;

namespace Faturamento.API.Application.Interfaces;

/// <summary>
/// Interface oficial para comunicação com o microsserviço de Estoque.
/// </summary>
public interface IEstoqueClient
{
    // Método para validação preventiva (Fail-Fast)
    Task<ProdutoEstoqueDto?> ObterPorCodigoAsync(string codigo);

    // Método para o fluxo assíncrono do RabbitMQ
    Task AbaterSaldoLoteAsync(IEnumerable<ItemAbatimento> itens);

    Task<bool> IsHealthyAsync();
}