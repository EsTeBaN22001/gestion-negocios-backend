using GestionNegocios.Api.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

// ─── Controllers ────────────────────────────────────────────────────────────
builder.Services.AddControllers();

// ─── Swagger / OpenAPI ──────────────────────────────────────────────────────
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo
    {
        Title = "Gestión Negocios API",
        Version = "v1",
        Description = "API REST del Sistema de Gestión de Negocios."
    });
});

// ─── Database — EF Core + MySQL ─────────────────────────────────────────────
var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseMySql(
        connectionString,
        new MySqlServerVersion(new Version(8, 0, 36)),
        mySqlOptions => mySqlOptions.EnableRetryOnFailure(
            maxRetryCount: 5,
            maxRetryDelay: TimeSpan.FromSeconds(10),
            errorNumbersToAdd: null)));

// ─── Health Checks ──────────────────────────────────────────────────────────
// El health check de BD se expone vía /api/health/db usando ApplicationDbContext.
builder.Services.AddHealthChecks();

// ─── CORS ───────────────────────────────────────────────────────────────────
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:4200"];

builder.Services.AddCors(options =>
{
    options.AddPolicy("DevelopmentPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// ─── Build ──────────────────────────────────────────────────────────────────
var app = builder.Build();

// ─── Swagger UI ─────────────────────────────────────────────────────────────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(options =>
    {
        options.SwaggerEndpoint("/swagger/v1/swagger.json", "Gestión Negocios API v1");
        options.RoutePrefix = "swagger";
    });
}

// ─── Middleware pipeline ─────────────────────────────────────────────────────
app.UseCors("DevelopmentPolicy");
app.UseAuthorization();

// ─── Health check endpoint ───────────────────────────────────────────────────
app.MapHealthChecks("/health");

// ─── Controllers ────────────────────────────────────────────────────────────
app.MapControllers();

// ─── Migraciones automáticas ────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        if (db.Database.CanConnect())
        {
            db.Database.Migrate();
        }
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogWarning(ex, "Aviso: No se pudieron aplicar las migraciones automáticas en el inicio.");
    }
}

app.Run();

