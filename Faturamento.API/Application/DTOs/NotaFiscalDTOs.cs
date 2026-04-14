namespace Faturamento.API.Application.DTOs;

public record CriarNotaRequest(IEnumerable<ItemNotaRequest> Itens);
public record ItemNotaRequest(string CodigoProduto, string DescricaoProduto, decimal Quantidade, decimal ValorUnitario);

public record NotaFiscalResponse(
    int Id, int Numero, DateTime DataEmissao, string Status,
    decimal ValorTotal, IEnumerable<ItemNotaResponse> Itens);

public record ItemNotaResponse(
    int Id, string CodigoProduto, string DescricaoProduto,
    decimal Quantidade, decimal ValorUnitario, decimal Subtotal);

/// <summary>Resposta estendida para o endpoint de impressão.</summary>
public record ImprimirResponse(
    NotaFiscalResponse Nota,
    bool EstoqueOnline,
    string Mensagem);
