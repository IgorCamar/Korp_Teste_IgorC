using Estoque.API.Application.DTOs;

namespace Estoque.API.Application.Interfaces;

public interface IProdutoService
{
    Task<ProdutoResponse> CriarAsync(CriarProdutoRequest request);
    Task<ProdutoResponse?> ObterPorIdAsync(int id);
    Task<ProdutoResponse?> ObterPorCodigoAsync(string codigo);
    Task<IEnumerable<ProdutoResponse>> ListarAsync();
    Task<ProdutoResponse> AtualizarAsync(int id, AtualizarProdutoRequest request);
    Task<ProdutoResponse> IncrementarSaldoAsync(int id, IncrementarSaldoRequest request);
    Task AbaterSaldoAsync(AbaterSaldoRequest request);
    Task AbaterSaldoLoteAsync(AbaterSaldoLoteRequest request);
    Task DeletarAsync(int id);
}
