using Faturamento.API.Domain.Entities;

namespace Faturamento.API.Domain.Interfaces;

public interface INotaFiscalRepository
{
    Task<NotaFiscal?> GetByIdAsync(int id);
    Task<NotaFiscal?> GetByNumeroAsync(int numero);
    Task<IEnumerable<NotaFiscal>> GetAllAsync();
    Task<int> GetProximoNumeroAsync();
    Task AddAsync(NotaFiscal nota);
    Task UpdateAsync(NotaFiscal nota);
}
