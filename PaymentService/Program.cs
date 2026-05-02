using Microsoft.EntityFrameworkCore;
using PaymentService.DAL;
using PaymentService.Services;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Payment Service application...");
    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services));

    builder.Services.AddDbContext<PaymentDbContext>(options =>
        options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

    builder.Services.AddHttpClient();
    builder.Services.AddScoped<IPaymentProcessor, PaymentProcessorService>();

    builder.Services.AddControllers();
    builder.Services.AddEndpointsApiExplorer();
    
    builder.Services.AddOpenApi(); 

    var app = builder.Build();

    // 5. Configure the HTTP request pipeline
    if (app.Environment.IsDevelopment())
    {
        app.MapOpenApi();
        
        // Map Scalar UI to interact with the OpenAPI spec
        app.MapScalarApiReference(options =>
        {
            options.WithTitle("Hospital Payment Service API")
                   .WithTheme(ScalarTheme.DeepSpace)
                   .WithDefaultHttpClient(ScalarTarget.CSharp, ScalarClient.HttpClient);
        });
    }

    app.UseSerilogRequestLogging();

    using (var scope = app.Services.CreateScope())
    {
        var db = scope.ServiceProvider.GetRequiredService<PaymentDbContext>();
        try
        {
            db.Database.Migrate();
            Log.Information("Database migrations applied successfully.");
            
            // Resync the PostgreSQL auto-increment sequence with manually seeded data
            db.Database.ExecuteSqlRaw("SELECT setval(pg_get_serial_sequence('payments', 'payment_id'), COALESCE((SELECT MAX(payment_id) FROM payments), 1), (SELECT MAX(payment_id) FROM payments) IS NOT NULL);");
            Log.Information("Database sequence synced with existing data.");
        }
        catch (Exception ex)
        {
            Log.Warning(ex, "Database migration skipped or failed. If tables already exist, this can safely be ignored.");
        }
    }

    app.UseAuthorization();
    app.MapControllers();

    app.Run();
}
catch (Exception ex) when (ex is not Microsoft.Extensions.Hosting.HostAbortedException)
{
    Log.Fatal(ex, "Application terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
