using Estoque.API.Domain.Entities;
using Estoque.API.Domain.Interfaces;
using Estoque.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Infrastructure.Repositories;

public class ProdutoRepository : IProdutoRepository
{
    private readonly EstoqueDbContext _context;

    public ProdutoRepository(EstoqueDbContext context)
    {
        _context = context;
    }

    public async Task<Produto?> GetByIdAsync(int id) =>
        await _context.Produtos.FindAsync(id);

    public async Task<Produto?> GetByCodigoAsync(string codigo) =>
        await _context.Produtos.FirstOrDefaultAsync(p => p.Codigo == codigo);

    public async Task<IEnumerable<Produto>> GetAllAsync() =>
        await _context.Produtos.OrderBy(p => p.Codigo).ToListAsync();

    public async Task AddAsync(Produto produto)
    {
        await _context.Produtos.AddAsync(produto);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(Produto produto)
    {
        _context.Produtos.Update(produto);
        await _context.SaveChangesAsync();
    }

    public async Task DeleteAsync(int id)
    {
        var produto = await GetByIdAsync(id);
        if (produto is not null)
        {
            _context.Produtos.Remove(produto);
            await _context.SaveChangesAsync();
        }
    }

    public async Task<bool> ExistsAsync(string codigo) =>
        await _context.Produtos.AnyAsync(p => p.Codigo == codigo.ToUpper());
}
