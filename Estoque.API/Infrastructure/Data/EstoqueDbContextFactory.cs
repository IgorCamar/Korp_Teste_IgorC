using Estoque.API.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Estoque.API.Infrastructure;

/// <summary>
/// Necessário para que o CLI `dotnet ef` consiga instanciar o DbContext
/// sem precisar subir a aplicação inteira (sem IHost).
/// Usado pelos comandos:
///   dotnet ef migrations add ...
///   dotnet ef database update
/// </summary>
public class EstoqueDbContextFactory : IDesignTimeDbContextFactory<EstoqueDbContext>
{
    public EstoqueDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<EstoqueDbContext>();

        // Connection string usada APENAS em tempo de design (CLI).
        // Em runtime, a string vem do appsettings.json / variável de ambiente.
        optionsBuilder.UseSqlServer(
            "Server=localhost,1433;Database=EstoqueDb;User Id=sa;Password=StrongPass@2024;TrustServerCertificate=True",
            sql => sql.MigrationsAssembly("Estoque.API")
        );

        return new EstoqueDbContext(optionsBuilder.Options);
    }
}
