using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

var builder = WebApplication.CreateBuilder(args);

// Registrar el DbContext con la cadena de conexión (usando SQL Server)
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));

// Agregar servicios para Razor Pages y sesión
builder.Services.AddRazorPages();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Configurar el pipeline de solicitud HTTP
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseSession(); // 👈 Habilitar middleware de sesión
app.UseAuthorization();

// Middleware para verificar la conexión a la base de datos
app.Use(async (context, next) =>
{
    using (var scope = app.Services.CreateScope())
    {
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();

        try
        {
            if (await dbContext.Database.CanConnectAsync())
            {
                logger.LogInformation("Conectado a la base de datos con éxito.");
                context.Items["DbConnectionMessage"] = "✅ Conectado a la base de datos con éxito.";
            }
            else
            {
                logger.LogWarning("No se pudo conectar a la base de datos.");
                context.Items["DbConnectionMessage"] = "⚠️ No se pudo conectar a la base de datos.";
            }
        }
        catch (Exception ex)
        {
            logger.LogError($"Error al conectar a la base de datos: {ex.Message}");
            context.Items["DbConnectionMessage"] = $"❌ Error al conectar a la base de datos: {ex.Message}";
        }
    }

    await next();
});

app.MapRazorPages();
app.MapDefaultControllerRoute();

app.Run();
