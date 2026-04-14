using Faturamento.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Faturamento.API.Infrastructure;

/// <summary>
/// Necessário para que o CLI `dotnet ef` consiga instanciar o DbContext
/// sem precisar subir a aplicação inteira (sem IHost).
/// Usado pelos comandos:
///   dotnet ef migrations add InitialFaturamento -p src/Infrastructure -s src/Faturamento.API
///   dotnet ef database update
/// </summary>
public class FaturamentoDbContextFactory : IDesignTimeDbContextFactory<FaturamentoDbContext>
{
    public FaturamentoDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<FaturamentoDbContext>();

        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=FaturamentoDb;User Id=sa;Password=StrongPass@2024;TrustServerCertificate=True",
            sql => sql.MigrationsAssembly("Faturamento.API")
        );

        return new FaturamentoDbContext(optionsBuilder.Options);
    }
}
