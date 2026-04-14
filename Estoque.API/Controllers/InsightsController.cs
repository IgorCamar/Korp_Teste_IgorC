using Estoque.API.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.API.Controllers;

[ApiController]
[Route("api/produtos")]
public class InsightsController : ControllerBase
{
    private readonly IEstoqueInsightsService _insightsService;

    public InsightsController(IEstoqueInsightsService insightsService)
        => _insightsService = insightsService;

    /// <summary>
    /// Agente RAG: recupera dados reais do estoque (Retrieval),
    /// monta prompt (Augmented) e chama Gemini API (Generation).
    /// </summary>
    [HttpGet("insights-ia")]
    public async Task<IActionResult> GetInsights()
    {
        try
        {
            var result = await _insightsService.AnalisarSaudeEstoqueAsync();
            return Ok(result);
        }
        catch (Exception ex)
        {
            return StatusCode(500, new { message = "Erro ao gerar análise.", detalhe = ex.Message });
        }
    }
}
