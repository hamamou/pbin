using Microsoft.EntityFrameworkCore;
using pbin;

var builder = WebApplication.CreateSlimBuilder(args);
var connectionString = GetConnectionString(builder);
builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));
var app = builder.Build();
ApplyDatabaseMigrations(app);

app.UseFileServer();
ConfigureRoutes(app);
app.Run();

string GetConnectionString(WebApplicationBuilder builder)
{
    var connectionString =
        Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
        ?? builder.Configuration.GetConnectionString("DefaultConnection");

    if (string.IsNullOrEmpty(connectionString))
    {
        throw new Exception("Database connection string is not set.");
    }

    return connectionString;
}

void ApplyDatabaseMigrations(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

void ConfigureRoutes(WebApplication app)
{
    app.MapGet("/", HandleRootRequest);

    app.MapGet("/{id}", HandlePasteRequest);

    app.MapPost("/", HandleCreatePasteRequest);
}

async Task HandleRootRequest(HttpContext context)
{
    var content = await GetHtmlTemplate("index");

    context.Response.ContentType = "text/html";
    context.Response.StatusCode = 200;
    await context.Response.WriteAsync(content);
}

async Task<IResult> HandlePasteRequest(HttpContext context, AppDbContext db, string id)
{
    if (!Guid.TryParse(id, out var guid))
    {
        return await ServeErrorPage(context, "Invalid paste ID format.");
    }

    var paste = await db.Pastes.FirstOrDefaultAsync(p => p.Guid == guid);
    if (paste is not null)
    {
        return await ServePastePage(context, paste);
    }

    return Results.NotFound();
}

async Task<IResult> ServeErrorPage(HttpContext context, string message)
{
    var errorContent = await GetHtmlTemplate("error");

    errorContent = errorContent.Replace("{{errorMessage}}", message);
    context.Response.StatusCode = 400;
    return Results.Content(errorContent, "text/html");
}

async Task<IResult> ServePastePage(HttpContext context, Paste paste)
{
    var pasteContent = await GetHtmlTemplate("paste");

    pasteContent = pasteContent.Replace("{{content}}", paste.Content);
    context.Response.StatusCode = 200;
    return Results.Content(pasteContent, "text/html");
}

async Task<IResult> HandleCreatePasteRequest(
    AppDbContext db,
    HttpContext context,
    PasteRequest request
)
{
    if (string.IsNullOrWhiteSpace(request.Content))
    {
        return Results.BadRequest("Content cannot be empty.");
    }

    var paste = new Paste { Content = request.Content };
    db.Pastes.Add(paste);
    await db.SaveChangesAsync();

    context.Response.StatusCode = 201;
    return Results.Json(new { id = paste.Guid });
}

static async Task<string> GetHtmlTemplate(string templateName)
{
    var templatePath = Path.Combine(
        Directory.GetCurrentDirectory(),
        "wwwroot",
        $"{templateName}.html"
    );
    return await File.ReadAllTextAsync(templatePath);
}
