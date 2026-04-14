using Faturamento.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Faturamento.API.Infrastructure.Data;

public class FaturamentoDbContext : DbContext
{
    public FaturamentoDbContext(DbContextOptions<FaturamentoDbContext> options) : base(options) { }

    public DbSet<NotaFiscal> NotasFiscais => Set<NotaFiscal>();
    public DbSet<ItemNota> ItensNota => Set<ItemNota>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.HasSequence<int>("NumeroNotaSequence", schema: "dbo")
            .StartsAt(1).IncrementsBy(1);

        modelBuilder.Entity<NotaFiscal>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Numero)
                .HasDefaultValueSql("NEXT VALUE FOR dbo.NumeroNotaSequence")
                .ValueGeneratedOnAdd();

            entity.HasIndex(e => e.Numero).IsUnique();
            entity.Property(e => e.DataEmissao).IsRequired();

            entity.Property(e => e.Status)
                .HasConversion<string>()
                .HasMaxLength(20)
                .IsRequired();

            entity.Property(e => e.ValorTotal)
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            // Optimistic concurrency — protege contra race condition (idempotência concorrente)
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();

            entity.HasMany(e => e.Itens)
                .WithOne()
                .HasForeignKey(i => i.NotaFiscalId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ItemNota>(entity =>
        {
            entity.HasKey(e => e.Id);
            entity.Property(e => e.CodigoProduto).IsRequired().HasMaxLength(50);
            entity.Property(e => e.DescricaoProduto).IsRequired().HasMaxLength(200);
            entity.Property(e => e.Quantidade).HasColumnType("decimal(18,4)").IsRequired();
            entity.Property(e => e.ValorUnitario).HasColumnType("decimal(18,4)").IsRequired();
        });
    }
}
