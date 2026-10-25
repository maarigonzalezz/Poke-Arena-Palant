
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using PokeArena.Application.Cards;
using PokeArena.Application.Locations;
using PokeArena.Infrastructure.Cards;
using PokeArena.Infrastructure.Locations;

// Configura la API, el acceso desde React y los servicios de búsqueda autenticada.
var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Conexión PostgreSQL compartida mediante
// un pool administrado por Npgsql.
var connectionString =
    builder.Configuration.GetConnectionString("Postgres")
    ?? throw new InvalidOperationException(
        "Falta ConnectionStrings:Postgres.");

builder.Services.AddSingleton(
    NpgsqlDataSource.Create(connectionString)
);

// Inyección de dependencias.
builder.Services.AddScoped<
    ICardSearchRepository,
    PostgresCardSearchRepository
>();

var cardsDirectory = Path.GetFullPath(
    Path.Combine(builder.Environment.ContentRootPath, "../../files/cards"));
builder.Services.AddScoped<ICardCreateRepository>(services =>
    new PostgresCardCreateRepository(
        services.GetRequiredService<NpgsqlDataSource>(),
        cardsDirectory));
builder.Services.AddScoped<
    ILocationRepository,
    PostgresLocationRepository
>();

// Configuración de autenticación JWT.
// Requiere un proveedor real configurado.
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

        options.Audience = audience;

        options.TokenValidationParameters =
            new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                RoleClaimType = "role",
                NameClaimType = "sub"
            };
    });

builder.Services.AddAuthorization();

var app = builder.Build();

Directory.CreateDirectory(cardsDirectory);

app.UseCors("ReactFrontend");

app.UseStaticFiles(new StaticFileOptions
{
    FileProvider = new Microsoft.Extensions.FileProviders.PhysicalFileProvider(
        cardsDirectory),
    RequestPath = "/files/cards"
});

app.UseAuthentication();
app.UseAuthorization();

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
