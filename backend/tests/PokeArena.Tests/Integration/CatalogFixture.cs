using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using Npgsql;

namespace PokeArena.Tests.Integration;

public sealed class IntegrationFactAttribute : FactAttribute
{
    public IntegrationFactAttribute()
    { if (Environment.GetEnvironmentVariable("POKEARENA_INTEGRATION_TESTS") != "1") Skip="Ejecutar scripts/test-integration.ps1 con Docker iniciado."; }
}

[CollectionDefinition("catalog-integration")]
public sealed class CatalogCollection : ICollectionFixture<CatalogFixture>;

/// <summary>API real con JWT firmado, PostgreSQL desechable y archivos fuera del catálogo del usuario.</summary>
public sealed class CatalogFixture : IAsyncLifetime
{
    private readonly string container = "pokearena-tests-" + Guid.NewGuid().ToString("N");
    private readonly string root = Path.Combine(Path.GetTempPath(), "pokearena-integration-" + Guid.NewGuid().ToString("N"));
    private readonly SymmetricSecurityKey key = new(RandomNumberGenerator.GetBytes(32));
    private WebApplicationFactory<Program>? app;
    public NpgsqlDataSource Database { get; private set; } = null!;
    public int StoredFiles => Directory.Exists(root) ? Directory.GetFiles(root,"*",SearchOption.AllDirectories).Length : 0;

    public async Task InitializeAsync()
    {
        if (Environment.GetEnvironmentVariable("POKEARENA_INTEGRATION_TESTS") != "1") return;
        try
        {
            var password = Convert.ToHexString(RandomNumberGenerator.GetBytes(24));
            await Docker("run", "--detach", "--rm", "--name", container, "--publish", "127.0.0.1::5432",
                "--env", "POSTGRES_PASSWORD="+password, "postgres:17");
            var port = (await Docker("port",container,"5432/tcp")).Trim().Split(':')[^1];
            var connection = $"Host=127.0.0.1;Port={port};Database=postgres;Username=postgres;Password={password}";
            Database = NpgsqlDataSource.Create(connection);
            var ready = false;
            for (var attempt=0; attempt<60; attempt++)
            {
                try { await using var check=await Database.OpenConnectionAsync(); ready=true; break; }
                catch (NpgsqlException) { await Task.Delay(500); }
            }
            if (!ready) throw new InvalidOperationException("PostgreSQL de pruebas no inició.");
            await Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"TestData/init.sql")));
            await Execute(await File.ReadAllTextAsync(Path.Combine(AppContext.BaseDirectory,"TestData/integrate.sql")));
            app = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Testing");
                builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string,string?>
                { ["ConnectionStrings:Postgres"]=connection, ["Storage:Root"]=root }));
                builder.ConfigureTestServices(services => services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
                {
                    options.Authority=null; options.MetadataAddress=null!; options.ConfigurationManager=null!;
                    options.TokenValidationParameters=new TokenValidationParameters
                    {
                        ValidateIssuer=true, ValidIssuer="catalog-tests", ValidateAudience=true, ValidAudience="catalog-tests",
                        ValidateLifetime=true, ValidateIssuerSigningKey=true, IssuerSigningKey=key,
                        RoleClaimType="role", NameClaimType="sub", ClockSkew=TimeSpan.Zero
                    };
                }));
            });
        }
        catch { await DisposeAsync(); throw; }
    }

    public HttpClient Client(string? role="ContentAdmin", string actor="admin-tests")
    {
        var client=app!.CreateClient();
        if (role is null) return client;
        var token=new JwtSecurityToken("catalog-tests","catalog-tests",
            new[] { new Claim("sub",actor), new Claim("role",role) },
            DateTime.UtcNow.AddMinutes(-1),DateTime.UtcNow.AddMinutes(10),new SigningCredentials(key,SecurityAlgorithms.HmacSha256));
        client.DefaultRequestHeaders.Authorization=new AuthenticationHeaderValue("Bearer",new JwtSecurityTokenHandler().WriteToken(token));
        return client;
    }

    public async Task Execute(string sql)
    { await using var command=Database.CreateCommand(sql); await command.ExecuteNonQueryAsync(); }

    public async Task<long> Count(string sql)
    { await using var command=Database.CreateCommand(sql); return Convert.ToInt64(await command.ExecuteScalarAsync()); }

    public async Task Reset()
    {
        await Execute("TRUNCATE cards, locations, card_search_sessions, idempotency_requests RESTART IDENTITY CASCADE; UPDATE card_code_sequences SET last_number=0;");
    }

    public async Task DisposeAsync()
    {
        if (app is not null) await app.DisposeAsync();
        if (Database is not null) await Database.DisposeAsync();
        if (Environment.GetEnvironmentVariable("POKEARENA_INTEGRATION_TESTS") == "1") await Docker("rm","--force",container);
        if (Directory.Exists(root)) Directory.Delete(root,true);
    }

    private static async Task<string> Docker(params string[] args)
    {
        var info=new ProcessStartInfo("docker") { RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false, CreateNoWindow=true };
        foreach (var arg in args) info.ArgumentList.Add(arg);
        using var process=Process.Start(info)!;
        var output=process.StandardOutput.ReadToEndAsync(); var error=process.StandardError.ReadToEndAsync();
        using var timeout=new CancellationTokenSource(TimeSpan.FromMinutes(2));
        await process.WaitForExitAsync(timeout.Token);
        if (process.ExitCode!=0) throw new InvalidOperationException("Docker: "+await error);
        return await output;
    }
}
