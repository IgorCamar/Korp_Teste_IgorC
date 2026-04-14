using Faturamento.API.Application.DTOs;
using Faturamento.API.Application.Interfaces;
using Faturamento.API.Application.Services;
using Microsoft.AspNetCore.Mvc;

namespace Faturamento.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class NotasFiscaisController : ControllerBase
{
    private readonly INotaFiscalService _service;
    public NotasFiscaisController(INotaFiscalService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var nota = await _service.ObterPorIdAsync(id);
        return nota is null ? NotFound() : Ok(nota);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarNotaRequest request)
    {
        try
        {
            var nota = await _service.CriarAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = nota.Id }, nota);
        }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
        catch (ArgumentException ex)         { return BadRequest(new { message = ex.Message }); }
    }

    /// <summary>
    /// Inicia a impressão via RabbitMQ.
    /// Retorna 202 Accepted com status Processando.
    /// A nota será fechada pelo consumer e o frontend notificado via SignalR.
    /// </summary>
    [HttpPost("{id:int}/imprimir")]
    public async Task<IActionResult> Imprimir(int id)
    {
        try
        {
            var result = await _service.ImprimirAsync(id);
            // 202 Accepted: operação iniciada, não concluída ainda
            return Accepted(result);
        }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
        catch (MensageriaIndisponivelException ex)
        {
            // RabbitMQ fora do ar — falha crítica, nota permanece Aberta
            return StatusCode(503, new
            {
                tipo    = "MENSAGERIA_INDISPONIVEL",
                message = ex.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            // Regra de negócio (nota não Aberta, race condition, etc.)
            return UnprocessableEntity(new
            {
                tipo    = "REGRA_NEGOCIO",
                message = ex.Message
            });
        }
    }

    [HttpPost("{id:int}/cancelar")]
    public async Task<IActionResult> Cancelar(int id)
    {
        try   { return Ok(await _service.CancelarAsync(id)); }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }
}
