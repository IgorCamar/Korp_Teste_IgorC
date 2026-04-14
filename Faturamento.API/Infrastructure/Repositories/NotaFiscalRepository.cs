using Faturamento.API.Domain.Entities;
using Faturamento.API.Domain.Interfaces;
using Faturamento.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.API.Infrastructure.Repositories;

public class NotaFiscalRepository : INotaFiscalRepository
{
    private readonly FaturamentoDbContext _context;

    public NotaFiscalRepository(FaturamentoDbContext context)
    {
        _context = context;
    }

    public async Task<NotaFiscal?> GetByIdAsync(int id) =>
        await _context.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Id == id);

    public async Task<NotaFiscal?> GetByNumeroAsync(int numero) =>
        await _context.NotasFiscais
            .Include(n => n.Itens)
            .FirstOrDefaultAsync(n => n.Numero == numero);

    public async Task<IEnumerable<NotaFiscal>> GetAllAsync() =>
        await _context.NotasFiscais
            .Include(n => n.Itens)
            .OrderByDescending(n => n.Numero)
            .ToListAsync();

    public async Task<int> GetProximoNumeroAsync()
    {
        var ultimo = await _context.NotasFiscais
            .MaxAsync(n => (int?)n.Numero) ?? 0;
        return ultimo + 1;
    }

    public async Task AddAsync(NotaFiscal nota)
    {
        await _context.NotasFiscais.AddAsync(nota);
        await _context.SaveChangesAsync();
    }

    public async Task UpdateAsync(NotaFiscal nota)
    {
        _context.NotasFiscais.Update(nota);
        await _context.SaveChangesAsync();
    }
}
