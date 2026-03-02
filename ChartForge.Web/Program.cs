using ChartForge.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using ChartForge.Web.Services;

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

builder.Services.AddHttpClient("N8nClient", client =>
{
    var webhookUrl = builder.Configuration["N8N:WebhookUrl"];
    if (!string.IsNullOrWhiteSpace(webhookUrl))
    {
        client.BaseAddress = new Uri(webhookUrl);
    }
});

builder.Services.AddScoped<ChatStateService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorComponents<ChartForge.Web.Components.App>()
    .AddInteractiveServerRenderMode();

app.Run();