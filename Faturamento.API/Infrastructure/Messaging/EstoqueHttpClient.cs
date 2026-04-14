using System.Text;
using System.Text.Json;
using System.Net.Http.Json;
using Faturamento.API.Application.DTOs;
using Faturamento.API.Application.Interfaces;

namespace Faturamento.API.Infrastructure.Messaging;

public class EstoqueHttpClient : IEstoqueClient
{
    private readonly HttpClient _http;
    private readonly ILogger<EstoqueHttpClient> _logger;

    public EstoqueHttpClient(HttpClient http, ILogger<EstoqueHttpClient> logger)
    {
        _http = http;
        _logger = logger;
    }

    public async Task<ProdutoEstoqueDto?> ObterPorCodigoAsync(string codigo)
    {
        try
        {
            var response = await _http.GetAsync($"api/Produtos/codigo/{codigo}");

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            response.EnsureSuccessStatusCode();

            return await response.Content.ReadFromJsonAsync<ProdutoEstoqueDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar estoque para o produto {Codigo}", codigo);
            return null;
        }
    }

    public async Task AbaterSaldoLoteAsync(IEnumerable<ItemAbatimento> itens)
    {
        var payload = new
        {
            itens = itens.Select(i => new
            {
                codigoProduto = i.CodigoProduto,
                quantidade = i.Quantidade
            })
        };

        var json = JsonSerializer.Serialize(payload);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await _http.PostAsync("/api/produtos/abater-lote", content);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            _logger.LogWarning("[EstoqueClient] Falha {Status}: {Body}", (int)response.StatusCode, body);
            throw new HttpRequestException($"Estoque.API retornou {(int)response.StatusCode}: {body}");
        }
    }

    public async Task<bool> IsHealthyAsync()
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
            var response = await _http.GetAsync("/health", cts.Token);
            return response.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogInformation("[EstoqueClient] Health check falhou: {Msg}", ex.Message);
            return false;
        }
    }
}