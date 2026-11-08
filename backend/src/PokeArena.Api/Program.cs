
using Microsoft.EntityFrameworkCore;
using Npgsql;
using PokeArena.Api;
using PokeArena.Application.Common;
using PokeArena.Infrastructure;
using PokeArena.Infrastructure.Persistence;
using PokeArena.Application.Cards;
using PokeArena.Application.Locations;
using PokeArena.Infrastructure.Cards;
using PokeArena.Application.Inventory;
using PokeArena.Infrastructure.Inventory;

// Punto de entrada: conecta HTTP, autenticación y repositorios del catálogo.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers(options => options.Filters.Add<CatalogExceptionFilter>());

// CORS permite que el frontend autorizado consulte la API desde otro origen.
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins(builder.Configuration.GetSection("Cors:Origins").Get<string[]>() ?? ["http://localhost:5173"])
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Npgsql reutiliza conexiones PostgreSQL mediante un pool compartido.
builder.Services.AddSingleton(services => NpgsqlDataSource.Create(
    services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres")
    ?? throw new InvalidOperationException("Falta ConnectionStrings:Postgres.")));

// Un DbContext por solicitud comparte seguimiento y transacciones sin compartir estado entre usuarios.
builder.Services.AddDbContext<PokeArenaDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

// Asocia cada interfaz de Application con su implementación en Infrastructure.
builder.Services.AddScoped<
    ICardSearchRepository,
    PostgresCardSearchRepository
>();

builder.Services.AddSingleton<IImageStore>(services => new CatalogImageStore(StorageRoot(
    services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>())));
// Los controladores usan servicios; los repositorios de escritura nacen dentro de la transacción.
builder.Services.AddSingleton<CatalogValidationService>();
builder.Services.AddScoped<CardService>();
builder.Services.AddScoped<LocationService>();
builder.Services.AddScoped<ICatalogTransaction, CatalogTransaction>();
builder.Services.AddHostedService<SearchSessionCleanup>();
builder.Services.AddScoped<IAbilityRepository, PostgresAbilityRepository>();

// El validador no conserva estado y puede compartirse entre solicitudes.
builder.Services.AddSingleton<InventoryValidationService>();
// Servicio y repositorio usan el DbContext de la misma solicitud HTTP.
builder.Services.AddScoped<InventoryService>();
builder.Services.AddScoped<IInventoryRepository, PostgresInventoryRepository>();

// Registra Google, emisión propia y autorización administrativa separada.
builder.Services.AddPlatformAuthentication(builder.Environment);

var app = builder.Build();

var filesDirectory = StorageRoot(app.Configuration, app.Environment);
Directory.CreateDirectory(filesDirectory);

app.UseCors("ReactFrontend");

// Las imágenes se sirven públicamente en /files; los endpoints administrativos requieren JWT.
app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        filesDirectory),
    RequestPath = "/files"
});

// Primero se identifica al usuario y después se comprueba su permiso para la operación.
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

// Activa las rutas declaradas en los controladores del catálogo.
app.MapControllers();

// Indica disponibilidad HTTP; no consulta PostgreSQL ni MongoDB.
app.MapGet("/api/health", () =>
{
    return Results.Ok(new
    {
        status = "OK",
        application = "PokeArena",
        message = "Backend funcionando correctamente"
    });
});

app.Run();

static string StorageRoot(IConfiguration config, IHostEnvironment environment) =>
    Path.GetFullPath(config["Storage:Root"] ?? Path.Combine(environment.ContentRootPath, "../../files"));

// Punto de entrada accesible para pruebas HTTP con WebApplicationFactory.
public partial class Program;
