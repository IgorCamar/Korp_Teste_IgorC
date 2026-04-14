using Estoque.API.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Estoque.API.Infrastructure.Data;

public class EstoqueDbContext : DbContext
{
    public EstoqueDbContext(DbContextOptions<EstoqueDbContext> options) : base(options) { }

    public DbSet<Produto> Produtos => Set<Produto>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Produto>(entity =>
        {
            entity.HasKey(e => e.Id);

            entity.Property(e => e.Codigo)
                .IsRequired()
                .HasMaxLength(50);

            entity.HasIndex(e => e.Codigo)
                .IsUnique();

            entity.Property(e => e.Descricao)
                .IsRequired()
                .HasMaxLength(200);

            entity.Property(e => e.Saldo)
                .HasColumnType("decimal(18,4)")
                .IsRequired();

            // Optimistic concurrency token (Epic 2, Task 2.3)
            entity.Property(e => e.RowVersion)
                .IsRowVersion()
                .IsConcurrencyToken();
        });
    }
}
