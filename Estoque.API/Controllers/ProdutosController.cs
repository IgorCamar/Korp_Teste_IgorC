using Estoque.API.Application.DTOs;
using Estoque.API.Application.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace Estoque.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public class ProdutosController : ControllerBase
{
    private readonly IProdutoService _service;
    public ProdutosController(IProdutoService service) => _service = service;

    [HttpGet]
    public async Task<IActionResult> GetAll() => Ok(await _service.ListarAsync());

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetById(int id)
    {
        var p = await _service.ObterPorIdAsync(id);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpGet("codigo/{codigo}")]
    public async Task<IActionResult> GetByCodigo(string codigo)
    {
        var p = await _service.ObterPorCodigoAsync(codigo);
        return p is null ? NotFound() : Ok(p);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CriarProdutoRequest request)
    {
        try
        {
            var p = await _service.CriarAsync(request);
            return CreatedAtAction(nameof(GetById), new { id = p.Id }, p);
        }
        catch (InvalidOperationException ex) { return Conflict(new { message = ex.Message }); }
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] AtualizarProdutoRequest request)
    {
        try   { return Ok(await _service.AtualizarAsync(id, request)); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }

    /// <summary>Incrementa o saldo de um produto existente.</summary>
    [HttpPost("{id:int}/incrementar")]
    public async Task<IActionResult> Incrementar(int id, [FromBody] IncrementarSaldoRequest request)
    {
        try   { return Ok(await _service.IncrementarSaldoAsync(id, request)); }
        catch (KeyNotFoundException ex)  { return NotFound(new { message = ex.Message }); }
        catch (ArgumentException ex)     { return BadRequest(new { message = ex.Message }); }
    }

    [HttpPost("abater")]
    public async Task<IActionResult> Abater([FromBody] AbaterSaldoRequest request)
    {
        try
        {
            await _service.AbaterSaldoAsync(request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpPost("abater-lote")]
    public async Task<IActionResult> AbaterLote([FromBody] AbaterSaldoLoteRequest request)
    {
        try
        {
            await _service.AbaterSaldoLoteAsync(request);
            return NoContent();
        }
        catch (KeyNotFoundException ex)      { return NotFound(new { message = ex.Message }); }
        catch (InvalidOperationException ex) { return UnprocessableEntity(new { message = ex.Message }); }
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        try   { await _service.DeletarAsync(id); return NoContent(); }
        catch (KeyNotFoundException ex) { return NotFound(new { message = ex.Message }); }
    }
}
