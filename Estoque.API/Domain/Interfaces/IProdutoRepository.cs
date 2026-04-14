using Estoque.API.Domain.Entities;

namespace Estoque.API.Domain.Interfaces;

public interface IProdutoRepository
{
    Task<Produto?> GetByIdAsync(int id);
    Task<Produto?> GetByCodigoAsync(string codigo);
    Task<IEnumerable<Produto>> GetAllAsync();
    Task AddAsync(Produto produto);
    Task UpdateAsync(Produto produto);
    Task DeleteAsync(int id);
    Task<bool> ExistsAsync(string codigo);
}
