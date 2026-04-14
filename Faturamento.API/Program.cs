using Faturamento.API.Application.Interfaces;
using Faturamento.API.Application.Services;
using Faturamento.API.Domain.Interfaces;
using Faturamento.API.Infrastructure.Data;
using Faturamento.API.Infrastructure.Hubs;
using Faturamento.API.Infrastructure.Messaging;
using Faturamento.API.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ── Banco ──────────────────────────────────────────────
builder.Services.AddDbContext<FaturamentoDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

builder.Services.AddScoped<INotaFiscalRepository, NotaFiscalRepository>();
builder.Services.AddScoped<INotaFiscalService, NotaFiscalService>();

// ── Estoque HTTP Client (com health check) ─────────────
builder.Services.AddHttpClient<EstoqueHttpClient>(client =>
{
    var url = builder.Configuration["Services:EstoqueUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(url);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// Registra também como IEstoqueClient para o consumer injetar via IServiceProvider
builder.Services.AddHttpClient<Faturamento.API.Application.Interfaces.IEstoqueClient, Faturamento.API.Infrastructure.Messaging.EstoqueHttpClient>(client =>
{
    var url = builder.Configuration["Services:EstoqueUrl"] ?? "http://localhost:5001";
    client.BaseAddress = new Uri(url);
    client.Timeout     = TimeSpan.FromSeconds(10);
});

// ── RabbitMQ Publisher (Singleton — conexão persistente) ──
builder.Services.AddSingleton<IRabbitMqPublisher>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var logger = sp.GetRequiredService<ILogger<RabbitMqPublisher>>();
    for (int attempt = 1; attempt <= 15; attempt++)
    {
        try { return new RabbitMqPublisher(config, logger); }
        catch (Exception ex)
        {
            logger.LogWarning("[RabbitMQ] Tentativa {N}/15: {Msg}. Aguardando 5s...", attempt, ex.Message);
            Thread.Sleep(5000);
        }
    }
    throw new Exception("Não foi possível conectar ao RabbitMQ após 15 tentativas.");
});

// ── RabbitMQ Consumer (BackgroundService) ─────────────
builder.Services.AddHostedService<NotaImpressaConsumer>();

// ── SignalR ────────────────────────────────────────────
builder.Services.AddSignalR();

// ── API ────────────────────────────────────────────────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAngular", policy =>
        policy
            .WithOrigins("http://localhost:4200", "http://localhost")
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()); // Necessário para SignalR WebSockets
});

var app = builder.Build();

// ── Banco: retry + fallback SQL ────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db     = scope.ServiceProvider.GetRequiredService<FaturamentoDbContext>();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

    for (int attempt = 1; attempt <= 15; attempt++)
    {
        try
        {
            logger.LogInformation("[Faturamento] Tentativa {A}/15 conectando ao banco...", attempt);
            db.Database.EnsureCreated();
            var pending = db.Database.GetPendingMigrations().ToList();
            if (pending.Any()) db.Database.Migrate();
            db.Database.ExecuteSqlRaw("SELECT TOP 1 Id FROM NotasFiscais WHERE 1=0");
            logger.LogInformation("[Faturamento] Banco inicializado.");
            break;
        }
        catch (Exception ex) when (ex.Message.Contains("Invalid object name"))
        {
            logger.LogWarning("[Faturamento] Criando tabelas via SQL...");
            try
            {
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sys.sequences WHERE name='NumeroNotaSequence')
                        CREATE SEQUENCE dbo.NumeroNotaSequence AS INT START WITH 1 INCREMENT BY 1;");
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='NotasFiscais' AND xtype='U')
                    BEGIN
                        CREATE TABLE NotasFiscais (
                            Id          INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            Numero      INT NOT NULL DEFAULT (NEXT VALUE FOR dbo.NumeroNotaSequence),
                            DataEmissao DATETIME2    NOT NULL,
                            Status      NVARCHAR(20) NOT NULL,
                            ValorTotal  DECIMAL(18,4) NOT NULL,
                            RowVersion  ROWVERSION   NOT NULL
                        );
                        CREATE UNIQUE INDEX IX_NotasFiscais_Numero ON NotasFiscais(Numero);
                    END");
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM sysobjects WHERE name='ItensNota' AND xtype='U')
                    BEGIN
                        CREATE TABLE ItensNota (
                            Id               INT IDENTITY(1,1) NOT NULL PRIMARY KEY,
                            NotaFiscalId     INT           NOT NULL,
                            CodigoProduto    NVARCHAR(50)  NOT NULL,
                            DescricaoProduto NVARCHAR(200) NOT NULL,
                            Quantidade       DECIMAL(18,4) NOT NULL,
                            ValorUnitario    DECIMAL(18,4) NOT NULL,
                            CONSTRAINT FK_ItensNota_NotasFiscais
                                FOREIGN KEY (NotaFiscalId) REFERENCES NotasFiscais(Id) ON DELETE CASCADE
                        );
                        CREATE INDEX IX_ItensNota_NotaFiscalId ON ItensNota(NotaFiscalId);
                    END");
                db.Database.ExecuteSqlRaw(@"
                    IF NOT EXISTS (SELECT * FROM __EFMigrationsHistory WHERE MigrationId='20240101000000_InitialFaturamento')
                        INSERT INTO __EFMigrationsHistory VALUES('20240101000000_InitialFaturamento','8.0.0')");
                logger.LogInformation("[Faturamento] Tabelas criadas.");
                break;
            }
            catch (Exception sqlEx) { logger.LogError(sqlEx, "Erro ao criar tabelas."); }
        }
        catch (Exception ex)
        {
            if (attempt == 15) throw;
            logger.LogWarning("[Faturamento] {Msg}. Aguardando 6s...", ex.Message);
            Thread.Sleep(6000);
        }
    }
}

// ── Middleware ─────────────────────────────────────────
app.UseSwagger();
app.UseSwaggerUI();
app.UseCors("AllowAngular");
app.UseAuthorization();
app.MapControllers();
app.MapHub<NotaFiscalHub>("/hubs/notafiscal");
app.MapGet("/health", () => Results.Ok(new { status = "healthy", timestamp = DateTime.UtcNow }));

app.Run();
