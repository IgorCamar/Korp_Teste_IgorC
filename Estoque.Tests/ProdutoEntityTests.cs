using Estoque.API.Domain.Entities;
using Xunit;

namespace Estoque.Tests;

public class ProdutoEntityTests
{
    [Fact]
    public void Produto_CriadoComSaldoPositivo_SaldoCorreto()
    {
        var produto = new Produto("PROD-01", "Produto Teste", 100m);
        Assert.Equal(100m, produto.Saldo);
    }

    [Fact]
    public void AbaterSaldo_QuantidadeValida_SaldoReduzido()
    {
        var produto = new Produto("PROD-01", "Produto Teste", 100m);
        produto.AbaterSaldo(30m);
        Assert.Equal(70m, produto.Saldo);
    }

    [Fact]
    public void AbaterSaldo_SaldoFicariaNegativo_LancaInvalidOperationException()
    {
        // Epic 2, Task 2.2: Saldo nunca pode ser negativo
        var produto = new Produto("PROD-01", "Produto Teste", 10m);

        var ex = Assert.Throws<InvalidOperationException>(
            () => produto.AbaterSaldo(50m));

        Assert.Contains("insuficiente", ex.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AbaterSaldo_QuantidadeNegativa_LancaArgumentException()
    {
        var produto = new Produto("PROD-01", "Produto Teste", 100m);

        Assert.Throws<ArgumentException>(
            () => produto.AbaterSaldo(-10m));
    }

    [Fact]
    public void AdicionarSaldo_QuantidadeValida_SaldoAumentado()
    {
        var produto = new Produto("PROD-01", "Produto Teste", 50m);
        produto.AdicionarSaldo(25m);
        Assert.Equal(75m, produto.Saldo);
    }

    [Fact]
    public void Produto_SaldoInicialNegativo_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Produto("PROD-01", "Produto Teste", -1m));
    }

    [Fact]
    public void Produto_CodigoVazio_LancaArgumentException()
    {
        Assert.Throws<ArgumentException>(
            () => new Produto("", "Produto Teste", 100m));
    }

    [Fact]
    public void Produto_CodigoNormalizado_UpperCase()
    {
        var produto = new Produto("prod-01", "Produto Teste", 100m);
        Assert.Equal("PROD-01", produto.Codigo);
    }
}
