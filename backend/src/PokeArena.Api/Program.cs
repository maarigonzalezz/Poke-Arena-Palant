
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PokeArena.Api;
using PokeArena.Application.Common;
using PokeArena.Infrastructure;
using PokeArena.Application.Cards;
using PokeArena.Application.Locations;
using PokeArena.Infrastructure.Cards;
using PokeArena.Infrastructure.Locations;

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

// Asocia cada interfaz de Application con su implementación en Infrastructure.
builder.Services.AddScoped<
    ICardSearchRepository,
    PostgresCardSearchRepository
>();

builder.Services.AddSingleton<IImageStore>(services => new CatalogImageStore(StorageRoot(
    services.GetRequiredService<IConfiguration>(), services.GetRequiredService<IHostEnvironment>())));
builder.Services.AddScoped<CatalogTransaction>();
builder.Services.AddHostedService<SearchSessionCleanup>();
builder.Services.AddScoped<ICardCreateRepository, PostgresCardCreateRepository>();
builder.Services.AddScoped<IAbilityRepository, PostgresAbilityRepository>();
builder.Services.AddScoped<
    ILocationRepository,
    PostgresLocationRepository
>();

// Valida JWT locales de desarrollo o los emitidos por el proveedor configurado.
var authority =
    builder.Configuration["Authentication:Authority"];

var audience =
    builder.Configuration["Authentication:Audience"];

builder.Services
    .AddAuthentication(
        JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        if (!string.IsNullOrWhiteSpace(authority))
        {
            options.Authority = authority;
        }

        if (!string.IsNullOrWhiteSpace(audience)) options.Audience = audience;
        // Conserva las claves de desarrollo que configura dotnet user-jwts.
        options.MapInboundClaims = false;
        options.TokenValidationParameters.ValidateIssuer = true;
        options.TokenValidationParameters.ValidateAudience = true;
        options.TokenValidationParameters.ValidateLifetime = true;
        options.TokenValidationParameters.ValidateIssuerSigningKey = true;
        // El claim role es el que consulta Authorize al exigir ContentAdmin.
        options.TokenValidationParameters.RoleClaimType = "role";
        options.TokenValidationParameters.NameClaimType = "sub";
        options.Events = new JwtBearerEvents
        {
            // 401 indica una credencial ausente, inválida o vencida.
            OnChallenge = context =>
            {
                context.HandleResponse();
                context.Response.StatusCode = 401;
                return context.Response.WriteAsJsonAsync(new { type="authentication_required", message="Debe iniciar sesión para continuar." });
            },
            // 403 indica que el usuario autenticado carece del rol requerido.
            OnForbidden = context =>
            {
                context.Response.StatusCode = 403;
                return context.Response.WriteAsJsonAsync(new { type="admin_required", message="Se requiere el rol ContentAdmin." });
            }
        };
    });

builder.Services.AddAuthorization();

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
