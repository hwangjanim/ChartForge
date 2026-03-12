using ChartForge.Infrastructure.Data;
using ChartForge.Infrastructure.Services;
using ChartForge.Web.Services;
using ChartForge.Core.Interfaces;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(
        builder.Configuration.GetConnectionString("DefaultConnection"),
        sqlOptions =>
        {
            sqlOptions.EnableRetryOnFailure(
                maxRetryCount: 5,
                maxRetryDelay: TimeSpan.FromSeconds(30),
                errorNumbersToAdd: null
            );
        }
    )
);

// SQL service
builder.Services.AddScoped<ISqlExecutionService, SqlExecutionService>();

builder.Services.AddScoped<IConversationService, ConversationService>();
builder.Services.AddScoped<ISqlQueryService, SqlQueryService>();
builder.Services.AddScoped<ChatStateService>();

builder.Services.AddHttpClient<IChatStreamService, N8nChatStreamService>(client =>
{
    client.BaseAddress = new Uri(builder.Configuration["N8N:WebhookUrl"] ?? "http://localhost:5015");
});

var app = builder.Build();

// Apply EF Core migrations automatically on startup.
using (var scope = app.Services.CreateScope())
{
    var factory = scope.ServiceProvider.GetRequiredService<IDbContextFactory<AppDbContext>>();
    await using var db = factory.CreateDbContext();
    await db.Database.MigrateAsync();
}

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.MapStaticAssets();
app.UseAntiforgery();

app.MapRazorComponents<ChartForge.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.MapFallback(() => Results.Redirect("/not-found"));
app.Run();
