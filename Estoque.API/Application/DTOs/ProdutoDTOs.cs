namespace Estoque.API.Application.DTOs;

public record CriarProdutoRequest(string Codigo, string Descricao, decimal SaldoInicial);
public record AtualizarProdutoRequest(string Descricao);
public record IncrementarSaldoRequest(decimal Quantidade);
public record AbaterSaldoRequest(string CodigoProduto, decimal Quantidade);
public record AbaterSaldoLoteRequest(IEnumerable<ItemAbatimento> Itens);
public record ItemAbatimento(string CodigoProduto, decimal Quantidade);
public record ProdutoResponse(int Id, string Codigo, string Descricao, decimal Saldo);
