namespace Faturamento.API.Application.DTOs;

public record ProdutoEstoqueDto(
	string Codigo,
	decimal Saldo
);