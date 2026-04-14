using Estoque.API.Application.Interfaces;
using Estoque.API.Application.Services;
using Estoque.API.Domain.Interfaces;
using Estoque.API.Infrastructure.Data;
using Estoque.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<EstoqueDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<IProdutoRepository, ProdutoRepository>();
builder.Services.AddScoped<IProdutoService, ProdutoService>();
builder.Services.AddScoped<IEstoqueInsightsService, EstoqueInsightsService>();
builder.Services.AddHttpClient();

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();

// Inicializa banco com retry robusto
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<EstoqueDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            logger.LogInformation("[Estoque] Tentativa {A}/15: conectando ao banco...", attempt);

            // Garante que o banco existe
            db.Database.EnsureCreated();

            // Tenta aplicar migrations pendentes
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Any())
            {
                logger.LogInformation("[Estoque] Aplicando {N} migration(s)...", pending.Count);
                db.Database.Migrate();
            }

            // Valida que a tabela existe executando uma query simples
            db.Database.ExecuteSqlRaw("SELECT TOP 1 Id FROM Produtos WHERE 1=0");
            logger.LogInformation("[Estoque] Banco inicializado com sucesso.");
            break;
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name"))
        {
            // Tabela não existe ainda — cria via SQL direto
            logger.LogWarning("[Estoque] Tabela não encontrada, criando via SQL...");
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='Produtos' AND xtype='U')
                    BEGIN
                        CREATE TABLE Produtos (
                            Id        INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            Codigo    NVARCHAR(50)  NOT NULL,
                            Descricao NVARCHAR(200) NOT NULL,
                            Saldo     DECIMAL(18,4) NOT NULL DEFAULT 0,
                            RowVersion ROWVERSION   NOT NULL
                        );
                        CREATE UNIQUE INDEX IX_Produtos_Codigo ON Produtos(Codigo);
                    END");

                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId='20240101000000_InitialEstoque')
                        INSERT INTO __EFMigrationsHistory(MigrationId, ProductVersion)
                        VALUES('20240101000000_InitialEstoque', '8.0.0')");

                logger.LogInformation("[Estoque] Tabelas criadas via SQL com sucesso.");
                break;
            }
            catch (Exception sqlEx)
            {
                logger.LogError(sqlEx, "[Estoque] Erro ao criar tabelas.");
            }
        }
        catch (Exception ex)
        {
            if (attempt == 15) throw;
            logger.LogWarning("[Estoque] Banco não disponível: {Msg}. Aguardando 6s...", ex.Message);
            Thread.Sleep(6000);
        }
    }
}

app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
