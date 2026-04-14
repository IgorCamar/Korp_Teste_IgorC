using System.Text;
using System.Text.Json;
using Estoque.API.Domain.Interfaces;

namespace Estoque.API.Application.Services;

public interface IEstoqueInsightsService
{
    Task<InsightResponse> AnalisarSaudeEstoqueAsync();
}

public record InsightResponse(string Analise, int TotalProdutos, int ProdutosBaixo, int ProdutosEsgotados, DateTime GeradoEm);

public class EstoqueInsightsService : IEstoqueInsightsService
{
    private readonly IProdutoRepository _repository;
    private readonly IConfiguration _config;
    private readonly IHttpClientFactory _httpFactory;
    private readonly ILogger<EstoqueInsightsService> _logger;

    public EstoqueInsightsService(
        IProdutoRepository repository,
        IConfiguration config,
        IHttpClientFactory httpFactory,
        ILogger<EstoqueInsightsService> logger)
    {
        _repository  = repository;
        _config      = config;
        _httpFactory = httpFactory;
        _logger      = logger;
    }

    public async Task<InsightResponse> AnalisarSaudeEstoqueAsync()
    {
        var todos     = (await _repository.GetAllAsync()).ToList();
        var baixo     = todos.Where(p => p.Saldo > 0 && p.Saldo < 10).OrderBy(p => p.Saldo).ToList();
        var esgotados = todos.Where(p => p.Saldo == 0).ToList();
        var normais   = todos.Where(p => p.Saldo >= 10).ToList();

        // =========================================================================
        // A REGRA DE OURO (CURTO-CIRCUITO):
        // Se não há itens críticos, retorna resposta instantânea sem chamar a IA.
        // =========================================================================
        if (!baixo.Any() && !esgotados.Any())
        {
            var mensagem = todos.Any() 
                ? "Seu estoque está perfeitamente saudável! Não há itens em nível crítico ou esgotados no momento."
                : "Seu catálogo está vazio. Comece a cadastrar produtos para gerenciar seus saldos.";

            return new InsightResponse(
                Analise:           mensagem,
                TotalProdutos:     todos.Count,
                ProdutosBaixo:     0,
                ProdutosEsgotados: 0,
                GeradoEm:          DateTime.UtcNow
            );
        }

        var contexto = new StringBuilder();
        contexto.AppendLine($"Total de produtos cadastrados: {todos.Count}");
        contexto.AppendLine($"Produtos com estoque normal (saldo >= 10): {normais.Count}");
        contexto.AppendLine($"Produtos com estoque baixo (saldo 1-9): {baixo.Count}");
        contexto.AppendLine($"Produtos esgotados (saldo 0): {esgotados.Count}");
        contexto.AppendLine();

        if (esgotados.Any())
        {
            contexto.AppendLine("PRODUTOS ESGOTADOS (prioridade critica):");
            foreach (var p in esgotados)
                contexto.AppendLine($"  - {p.Codigo}: {p.Descricao} (saldo: 0)");
            contexto.AppendLine();
        }

        if (baixo.Any())
        {
            contexto.AppendLine("PRODUTOS COM ESTOQUE BAIXO (atencao):");
            foreach (var p in baixo)
                contexto.AppendLine($"  - {p.Codigo}: {p.Descricao} (saldo: {p.Saldo})");
            contexto.AppendLine();
        }

        if (normais.Any())
        {
            contexto.AppendLine("AMOSTRA DE PRODUTOS COM ESTOQUE ADEQUADO (5 primeiros):");
            foreach (var p in normais.Take(5))
                contexto.AppendLine($"  - {p.Codigo}: {p.Descricao} (saldo: {p.Saldo})");
        }

        var prompt = $@"Voce e um analista especialista em gestao de estoque para um sistema ERP corporativo.
Com base nos dados reais do estoque abaixo, gere uma analise executiva objetiva e acionavel em portugues.

DADOS DO ESTOQUE:
{contexto}

INSTRUCOES:
- Seja direto e profissional, sem introducoes genericas
- Destaque os itens criticos (esgotados e baixo estoque) com sugestoes concretas
- Se o estoque estiver saudavel, confirme isso de forma positiva
- Limite a resposta a 5 paragrafos concisos
- Nao use markdown com asteriscos - use apenas texto simples
- Finalize com uma recomendacao de acao imediata";

        var analise = await ChamarGeminiAsync(prompt);

        return new InsightResponse(
            Analise:           analise,
            TotalProdutos:     todos.Count,
            ProdutosBaixo:     baixo.Count,
            ProdutosEsgotados: esgotados.Count,
            GeradoEm:          DateTime.UtcNow
        );
    }

    private async Task<string> ChamarGeminiAsync(string prompt)
    {
        var apiKey = _config["AI:GeminiApiKey"]?.Trim();

        if (string.IsNullOrWhiteSpace(apiKey))
        {
            _logger.LogWarning("Chave Gemini nao configurada. Retornando analise simulada.");
            return "Analise de IA nao disponivel: configure AI__GeminiApiKey nas variaveis de ambiente. " +
                   "Obtenha uma chave gratuita em https://aistudio.google.com/app/apikey e consulte o README.";
        }

        try
        {
            var client = _httpFactory.CreateClient("gemini");
            
            // Lê o modelo do appsettings/.env. Se não encontrar, usa o 2.5 flash como padrão de segurança.
            var modelo = _config["AI:GeminiModel"]?.Trim() ?? "gemini-2.5-flash";

            var url = $"https://generativelanguage.googleapis.com/v1beta/models/{modelo}:generateContent?key={apiKey}";

            var body = JsonSerializer.Serialize(new
            {
                contents = new[]
                {
                    new { parts = new[] { new { text = prompt } } }
                },
                generationConfig = new
                {
                    temperature     = 0.4,
                    maxOutputTokens = 1024
                }
            });

            var response = await client.PostAsync(url, new StringContent(body, Encoding.UTF8, "application/json"));

            if (!response.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync();
                _logger.LogError("Gemini retornou {Status}: {Err}", (int)response.StatusCode, err);
                return "Falha ao obter analise da IA. Tente novamente em instantes.";
            }

            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            var text = doc.RootElement
                .GetProperty("candidates")[0]
                .GetProperty("content")
                .GetProperty("parts")[0]
                .GetProperty("text")
                .GetString();

            return text ?? "Resposta vazia da IA.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excecao ao chamar Gemini API.");
            return "Erro interno ao chamar o servico de IA. Verifique os logs do servidor.";
        }
    }
}