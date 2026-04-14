using Faturamento.API.Application.DTOs;

namespace Faturamento.API.Application.Interfaces;

public interface INotaFiscalService
{
    Task<NotaFiscalResponse> CriarAsync(CriarNotaRequest request);
    Task<NotaFiscalResponse?> ObterPorIdAsync(int id);
    Task<IEnumerable<NotaFiscalResponse>> ListarAsync();
    Task<ImprimirResponse> ImprimirAsync(int notaFiscalId);
    Task<NotaFiscalResponse> CancelarAsync(int notaFiscalId);
}
