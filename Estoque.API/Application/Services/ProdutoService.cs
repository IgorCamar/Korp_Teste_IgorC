using Estoque.API.Application.DTOs;
using Estoque.API.Application.Interfaces;
using Estoque.API.Domain.Entities;
using Estoque.API.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Application.Services;

public class ProdutoService : IProdutoService
{
    private readonly IProdutoRepository _repository;
    private readonly ILogger<ProdutoService> _logger;

    public ProdutoService(IProdutoRepository repository, ILogger<ProdutoService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task<ProdutoResponse> CriarAsync(CriarProdutoRequest request)
    {
        if (await _repository.ExistsAsync(request.Codigo))
            throw new InvalidOperationException($"Produto com código '{request.Codigo}' já existe.");

        var produto = new Produto(request.Codigo, request.Descricao, request.SaldoInicial);
        await _repository.AddAsync(produto);
        _logger.LogInformation("Produto {Codigo} criado. Saldo: {Saldo}", produto.Codigo, produto.Saldo);
        return ToResponse(produto);
    }

    public async Task<ProdutoResponse?> ObterPorIdAsync(int id)
    {
        var p = await _repository.GetByIdAsync(id);
        return p is null ? null : ToResponse(p);
    }

    public async Task<ProdutoResponse?> ObterPorCodigoAsync(string codigo)
    {
        var p = await _repository.GetByCodigoAsync(codigo.ToUpper());
        return p is null ? null : ToResponse(p);
    }

    public async Task<IEnumerable<ProdutoResponse>> ListarAsync()
    {
        var lista = await _repository.GetAllAsync();
        return lista.Select(ToResponse);
    }

    public async Task<ProdutoResponse> AtualizarAsync(int id, AtualizarProdutoRequest request)
    {
        var produto = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Produto {id} não encontrado.");
        produto.Atualizar(request.Descricao);
        await _repository.UpdateAsync(produto);
        return ToResponse(produto);
    }

    public async Task<ProdutoResponse> IncrementarSaldoAsync(int id, IncrementarSaldoRequest request)
    {
        var produto = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Produto {id} não encontrado.");

        produto.IncrementarSaldo(request.Quantidade);
        await _repository.UpdateAsync(produto);
        _logger.LogInformation("Saldo de {Codigo} incrementado em {Qtd}. Novo saldo: {Saldo}",
            produto.Codigo, request.Quantidade, produto.Saldo);
        return ToResponse(produto);
    }

    public async Task AbaterSaldoAsync(AbaterSaldoRequest request)
    {
        var produto = await _repository.GetByCodigoAsync(request.CodigoProduto.ToUpper())
            ?? throw new KeyNotFoundException($"Produto '{request.CodigoProduto}' não encontrado.");
        try
        {
            produto.AbaterSaldo(request.Quantidade);
            await _repository.UpdateAsync(produto);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new InvalidOperationException(
                $"Conflito de concorrência ao atualizar saldo de '{request.CodigoProduto}'. Tente novamente.");
        }
    }

    public async Task AbaterSaldoLoteAsync(AbaterSaldoLoteRequest request)
    {
        var codigos = request.Itens.Select(i => i.CodigoProduto.ToUpper()).ToList();

        var duplicados = codigos.GroupBy(c => c).Where(g => g.Count() > 1).Select(g => g.Key).ToList();
        if (duplicados.Any())
            throw new InvalidOperationException($"Itens duplicados: {string.Join(", ", duplicados)}");

        var produtos = new List<(Produto Produto, decimal Quantidade)>();
        foreach (var item in request.Itens)
        {
            var produto = await _repository.GetByCodigoAsync(item.CodigoProduto.ToUpper())
                ?? throw new KeyNotFoundException($"Produto '{item.CodigoProduto}' não encontrado.");

            if (produto.Saldo - item.Quantidade < 0)
                throw new InvalidOperationException(
                    $"Saldo insuficiente para '{item.CodigoProduto}'. " +
                    $"Saldo: {produto.Saldo:F4}, Solicitado: {item.Quantidade:F4}");

            produtos.Add((produto, item.Quantidade));
        }

        foreach (var (produto, quantidade) in produtos)
        {
            produto.AbaterSaldo(quantidade);
            await _repository.UpdateAsync(produto);
        }
    }

    public async Task DeletarAsync(int id)
    {
        var produto = await _repository.GetByIdAsync(id)
            ?? throw new KeyNotFoundException($"Produto {id} não encontrado.");
        await _repository.DeleteAsync(id);
    }

    private static ProdutoResponse ToResponse(Produto p) =>
        new(p.Id, p.Codigo, p.Descricao, p.Saldo);
}
