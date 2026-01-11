using BingoBenji.Data;
using BingoBenji.Services;
using Microsoft.EntityFrameworkCore;
using QuestPDF.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// MVC
builder.Services.AddControllersWithViews();

// DbContext (SQL Server / LocalDB)
builder.Services.AddDbContext<BingoBenjiDbContext>(options =>
{
    var cs = builder.Configuration.GetConnectionString("BingoBenjiDb");

    // Configuración típica para LocalDB / SQL Server
    options.UseSqlServer(cs, sql =>
    {
        sql.EnableRetryOnFailure(
            maxRetryCount: 3,
            maxRetryDelay: TimeSpan.FromSeconds(3),
            errorNumbersToAdd: null);
    });
});

// Servicios
builder.Services.AddScoped<GenerationCodeService>();
builder.Services.AddScoped<BingoCardGenerator>();
builder.Services.AddScoped<PdfService>();
builder.Services.AddScoped<ZipService>();
builder.Services.AddSingleton<ZipBatchJobService>();


// QuestPDF
QuestPDF.Settings.License = LicenseType.Community;

var app = builder.Build();

// ? Verificación de conexión al arrancar (te ayuda a detectar si el Server/Database está mal)
using (var scope = app.Services.CreateScope())
{
    try
    {
        var db = scope.ServiceProvider.GetRequiredService<BingoBenjiDbContext>();
        var canConnect = db.Database.CanConnect();

        if (!canConnect)
            Console.WriteLine("? BingoBenji: NO se pudo conectar a la base de datos. Revisa ConnectionStrings:BingoBenjiDb.");
        else
            Console.WriteLine("? BingoBenji: conexión a BD OK.");
    }
    catch (Exception ex)
    {
        Console.WriteLine("? BingoBenji: error al conectar con BD.");
        Console.WriteLine(ex.ToString());
    }
}

// Middleware
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    // Útil para ver errores completos en dev
    app.UseDeveloperExceptionPage();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();
