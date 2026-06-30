using WorshipSearchWeb.Prompts;
using WorshipSearchWeb.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = null;
        options.JsonSerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
    });

builder.Services.AddHttpClient<ILlmKeyService, LlmKeyService>();
builder.Services.AddHttpClient<IGroqService, GroqService>();
builder.Services.AddHttpClient<IMusicSearchService, MusicSearchService>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(15);
});
builder.Services.AddHttpClient<IMusicHtmlParser, MusicHtmlParser>(client =>
{
    client.DefaultRequestHeaders.UserAgent.ParseAdd(
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 Chrome/120.0 Safari/537.36");
    client.Timeout = TimeSpan.FromSeconds(20);
});

builder.Services.AddSingleton<IMusicJsonGenerator, MusicJsonGenerator>();
builder.Services.AddSingleton<IPromptBuilder, PromptBuilder>();
builder.Services.AddScoped<IMusicEnrichmentService, MusicEnrichmentService>();
builder.Services.AddSingleton<IMusicRepository, MusicRepository>();
builder.Services.AddSingleton<ISearchRestrictionsService, SearchRestrictionsService>();
builder.Services.AddSingleton<ISearchHistoryService, SearchHistoryService>();
builder.Services.AddSingleton<IJsonSearchEngine, JsonSearchEngine>();
builder.Services.AddSingleton<IAppLogger, FileAppLogger>();
builder.Services.AddScoped<IQueryExpansionService, QueryExpansionService>();
builder.Services.AddScoped<ISearchIndexClassifier, SearchIndexClassifierService>();
builder.Services.AddScoped<ISearchDocumentService, SearchDocumentService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseRouting();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var jsonFolder = Path.Combine(app.Environment.ContentRootPath, "json");
Directory.CreateDirectory(jsonFolder);
Directory.CreateDirectory(Path.Combine(app.Environment.ContentRootPath, "logs"));

app.Run();
