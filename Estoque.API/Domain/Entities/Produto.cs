namespace Estoque.API.Domain.Entities;

public class Produto
{
    public int Id { get; private set; }
    public string Codigo { get; private set; } = null!;
    public string Descricao { get; private set; } = null!;
    public decimal Saldo { get; private set; }
    public byte[] RowVersion { get; private set; } = null!;

    protected Produto() { }

    public Produto(string codigo, string descricao, decimal saldoInicial)
    {
        if (string.IsNullOrWhiteSpace(codigo))
            throw new ArgumentException("Código não pode ser vazio.", nameof(codigo));
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição não pode ser vazia.", nameof(descricao));
        if (saldoInicial < 0)
            throw new ArgumentException("Saldo inicial não pode ser negativo.", nameof(saldoInicial));

        Codigo    = codigo.Trim().ToUpper();
        Descricao = descricao.Trim();
        Saldo     = saldoInicial;
    }

    public void Atualizar(string descricao)
    {
        if (string.IsNullOrWhiteSpace(descricao))
            throw new ArgumentException("Descrição não pode ser vazia.", nameof(descricao));
        Descricao = descricao.Trim();
    }

    public void IncrementarSaldo(decimal quantidade)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade a incrementar deve ser positiva.", nameof(quantidade));
        Saldo += quantidade;
    }

    public void AbaterSaldo(decimal quantidade)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade a abater deve ser positiva.", nameof(quantidade));
        if (Saldo - quantidade < 0)
            throw new InvalidOperationException(
                $"Saldo insuficiente para '{Codigo}'. Saldo: {Saldo}, Solicitado: {quantidade}");
        Saldo -= quantidade;
    }

    public void AdicionarSaldo(decimal quantidade)
    {
        if (quantidade <= 0)
            throw new ArgumentException("Quantidade a adicionar deve ser positiva.", nameof(quantidade));
        Saldo += quantidade;
    }

    // Mantido para compatibilidade
    public void AtualizarDescricao(string descricao) => Atualizar(descricao);
}
