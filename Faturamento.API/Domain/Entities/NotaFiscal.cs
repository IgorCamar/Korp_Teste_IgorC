namespace Faturamento.API.Domain.Entities;

public enum StatusNota
{
    Aberta      = 1,
    Processando = 2,   // Mensagem publicada no RabbitMQ, aguardando confirmação do Estoque
    Fechada     = 3,
    Cancelada   = 4
}

public class NotaFiscal
{
    public int Id { get; private set; }
    public int Numero { get; private set; }
    public DateTime DataEmissao { get; private set; }
    public StatusNota Status { get; private set; }
    public decimal ValorTotal { get; private set; }

    // Optimistic concurrency — impede que duas requisições simultâneas fechem a mesma nota
    public byte[] RowVersion { get; private set; } = null!;

    private readonly List<ItemNota> _itens = new();
    public IReadOnlyCollection<ItemNota> Itens => _itens.AsReadOnly();

    protected NotaFiscal() { }

    public NotaFiscal(IEnumerable<ItemNota> itens)
    {
        Status      = StatusNota.Aberta;
        DataEmissao = DateTime.UtcNow;

        var lista = itens?.ToList()
            ?? throw new ArgumentNullException(nameof(itens));
        if (!lista.Any())
            throw new InvalidOperationException("A nota deve ter pelo menos um item.");

        _itens.AddRange(lista);
        RecalcularTotal();
    }

    private void RecalcularTotal() =>
        ValorTotal = _itens.Sum(i => i.Quantidade * i.ValorUnitario);

    /// <summary>
    /// Marca nota como Processando após publicar no RabbitMQ.
    /// Só pode ser chamado quando Status = Aberta.
    /// RowVersion garante idempotência: se duas requisições chegarem ao mesmo tempo,
    /// a segunda receberá DbUpdateConcurrencyException.
    /// </summary>
    public void IniciarProcessamento()
    {
        if (Status != StatusNota.Aberta)
            throw new InvalidOperationException(
                $"Apenas notas Abertas podem ser enviadas para impressão. Status atual: {Status}.");
        Status = StatusNota.Processando;
    }

    /// <summary>Chamado pelo consumer do RabbitMQ após confirmação do Estoque.</summary>
    public void ConfirmarImpressao()
    {
        if (Status != StatusNota.Processando && Status != StatusNota.Aberta)
            throw new InvalidOperationException(
                $"Estado inválido para confirmar impressão. Status atual: {Status}.");
        Status = StatusNota.Fechada;
    }

    /// <summary>Reverte para Aberta se o consumer falhar ao processar.</summary>
    public void ReverterParaAberta()
    {
        Status = StatusNota.Aberta;
    }

    public void Cancelar()
    {
        if (Status == StatusNota.Fechada)
            throw new InvalidOperationException("Notas fechadas não podem ser canceladas.");
        if (Status == StatusNota.Cancelada)
            throw new InvalidOperationException("Esta nota já está cancelada.");
        Status = StatusNota.Cancelada;
    }
}

public class ItemNota
{
    public int Id { get; private set; }
    public int NotaFiscalId { get; private set; }
    public string CodigoProduto { get; private set; } = null!;
    public string DescricaoProduto { get; private set; } = null!;
    public decimal Quantidade { get; private set; }
    public decimal ValorUnitario { get; private set; }
    public decimal Subtotal => Quantidade * ValorUnitario;

    protected ItemNota() { }

    public ItemNota(string codigoProduto, string descricaoProduto, decimal quantidade, decimal valorUnitario)
    {
        if (string.IsNullOrWhiteSpace(codigoProduto))
            throw new ArgumentException("Código do produto é obrigatório.", nameof(codigoProduto));
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade deve ser positiva.", nameof(quantidade));
        if (valorUnitario <= 0)
            throw new ArgumentException("Valor unitário deve ser positivo.", nameof(valorUnitario));

        CodigoProduto    = codigoProduto.Trim().ToUpper();
        DescricaoProduto = descricaoProduto.Trim();
        Quantidade       = quantidade;
        ValorUnitario    = valorUnitario;
    }
}
