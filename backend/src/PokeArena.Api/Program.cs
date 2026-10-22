
var builder = WebApplication.CreateBuilder(args);

// Registrar los controladores de la API
builder.Services.AddControllers();

// Configurar CORS para permitir solicitudes de React
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

var app = builder.Build();

// Habilitar la política CORS
app.UseCors("ReactFrontend");

// Controladores de la API
app.MapControllers();

// Endpoint de prueba para verificar conectividad
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
