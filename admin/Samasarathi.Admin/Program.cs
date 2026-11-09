using Samasarathi.Admin.Components;
using Samasarathi.Admin.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

// One AdminSession per browser tab (Blazor "circuit"); ApiClient sends its token with every API call.
builder.Services.AddScoped<AdminSession>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddHttpClient<ApiClient>(c =>
    c.BaseAddress = new Uri(builder.Configuration["Api:BaseUrl"]
        ?? throw new InvalidOperationException("Set Api:BaseUrl to the Samasarathi API address.")));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
}
app.UseStatusCodePagesWithReExecute("/not-found", createScopeForStatusCodePages: true);
app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();
