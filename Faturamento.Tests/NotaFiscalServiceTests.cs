using Faturamento.API.Application.Interfaces;
using Faturamento.API.Application.DTOs;
using Faturamento.API.Application.Services;
using Faturamento.API.Domain.Entities;
using Faturamento.API.Domain.Interfaces;
using Faturamento.API.Infrastructure.Messaging;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Faturamento.Tests;

public class NotaFiscalServiceTests
{
    private readonly Mock<INotaFiscalRepository> _repoMock;
    private readonly Mock<IRabbitMqPublisher> _rabbitMock;
    private readonly Mock<IEstoqueClient> _estoqueMock;
    private readonly Mock<ILogger<NotaFiscalService>> _loggerMock;
    private readonly NotaFiscalService _service;

    public NotaFiscalServiceTests()
    {
        _repoMock = new Mock<INotaFiscalRepository>();
        _rabbitMock = new Mock<IRabbitMqPublisher>();
        _estoqueMock = new Mock<IEstoqueClient>();
        _loggerMock = new Mock<ILogger<NotaFiscalService>>();

        _service = new NotaFiscalService(
            _repoMock.Object,
            _rabbitMock.Object,
            _estoqueMock.Object,
            _loggerMock.Object);
    }

    // ─── CriarAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CriarAsync_ComItensValidos_RetornaNota()
    {
        // ARRANGE
        var request = new CriarNotaRequest(new[]
        {
            new ItemNotaRequest("PROD-01", "Produto A", 2, 50m),
            new ItemNotaRequest("PROD-02", "Produto B", 1, 100m)
        });

        // SETUP: Simula que o estoque tem saldo para ambos os produtos (Novo requisito do refactor)
        _estoqueMock.Setup(e => e.ObterPorCodigoAsync(It.IsAny<string>()))
                    .ReturnsAsync((string codigo) => new ProdutoEstoqueDto(codigo, 1000m));

        _repoMock.Setup(r => r.AddAsync(It.IsAny<NotaFiscal>()))
                 .Returns(Task.CompletedTask);

        // ACT
        var result = await _service.CriarAsync(request);

        // ASSERT
        Assert.NotNull(result);
        Assert.Equal("Aberta", result.Status);
        Assert.Equal(200m, result.ValorTotal); // 2*50 + 1*100
        _repoMock.Verify(r => r.AddAsync(It.IsAny<NotaFiscal>()), Times.Once);
    }

    [Fact]
    public async Task CriarAsync_ComSaldoInsuficiente_LancaInvalidOperationException()
    {
        // ARRANGE
        var request = new CriarNotaRequest(new[]
        {
            new ItemNotaRequest("PROD-LIMITADO", "Produto Raro", 10, 50m)
        });

        // SETUP: Simula saldo menor que o pedido
        _estoqueMock.Setup(e => e.ObterPorCodigoAsync("PROD-LIMITADO"))
                    .ReturnsAsync(new ProdutoEstoqueDto("PROD-LIMITADO", 2m));

        // ACT & ASSERT
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CriarAsync(request));

        Assert.Contains("Saldo insuficiente", ex.Message);
    }

    [Fact]
    public async Task CriarAsync_ComItensDuplicados_LancaInvalidOperationException()
    {
        var request = new CriarNotaRequest(new[]
        {
            new ItemNotaRequest("PROD-01", "Produto A", 2, 50m),
            new ItemNotaRequest("PROD-01", "Produto A dup", 1, 50m)
        });

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CriarAsync(request));

        Assert.Contains("duplicados", ex.Message);
    }

    [Fact]
    public async Task CriarAsync_SemItens_LancaInvalidOperationException()
    {
        var request = new CriarNotaRequest(Array.Empty<ItemNotaRequest>());

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.CriarAsync(request));
    }

    // ─── ImprimirAsync ─────────────────────────────────────────────

    [Fact]
    public async Task ImprimirAsync_NotaAberta_RabbitOnline_EstoqueOnline_RetornaProcessando()
    {
        var nota = CriarNotaFake(StatusNota.Aberta);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(nota);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<NotaFiscal>())).Returns(Task.CompletedTask);
        _rabbitMock.Setup(r => r.IsAvailable()).Returns(true);
        _estoqueMock.Setup(e => e.IsHealthyAsync()).ReturnsAsync(true);

        var result = await _service.ImprimirAsync(1);

        Assert.Equal("Processando", result.Nota.Status);
        Assert.True(result.EstoqueOnline);
        _rabbitMock.Verify(
            r => r.Publish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()),
            Times.Once);
    }

    [Fact]
    public async Task ImprimirAsync_EstoqueOffline_AindaPublicaNaFila_RetornaProcessando()
    {
        var nota = CriarNotaFake(StatusNota.Aberta);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(nota);
        _repoMock.Setup(r => r.UpdateAsync(It.IsAny<NotaFiscal>())).Returns(Task.CompletedTask);
        _rabbitMock.Setup(r => r.IsAvailable()).Returns(true);
        _estoqueMock.Setup(e => e.IsHealthyAsync()).ReturnsAsync(false);

        var result = await _service.ImprimirAsync(1);

        Assert.False(result.EstoqueOnline);
        Assert.Equal("Processando", result.Nota.Status);
    }

    [Fact]
    public async Task ImprimirAsync_NotaJaFechada_LancaInvalidOperationException()
    {
        var nota = CriarNotaFake(StatusNota.Fechada);
        _repoMock.Setup(r => r.GetByIdAsync(1)).ReturnsAsync(nota);

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _service.ImprimirAsync(1));

        Assert.Contains("Aberta", ex.Message);
        _rabbitMock.Verify(
            r => r.Publish(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<object>()),
            Times.Never);
    }

    // ─── Helpers ────────────────────────────────────────────────────

    private static NotaFiscal CriarNotaFake(StatusNota status)
    {
        var itens = new List<ItemNota>
        {
            new("PROD-01", "Produto Teste", 2, 50m)
        };
        var nota = new NotaFiscal(itens);

        switch (status)
        {
            case StatusNota.Processando:
                nota.IniciarProcessamento();
                break;
            case StatusNota.Fechada:
                nota.IniciarProcessamento();
                nota.ConfirmarImpressao();
                break;
            case StatusNota.Cancelada:
                nota.Cancelar();
                break;
        }

        return nota;
    }
}