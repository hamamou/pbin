using Microsoft.EntityFrameworkCore;
using pbin;

var builder = WebApplication.CreateBuilder(args);

var connectionString =
    Environment.GetEnvironmentVariable("POSTGRES_CONNECTION_STRING")
    ?? builder.Configuration.GetConnectionString("DefaultConnection");

if (string.IsNullOrEmpty(connectionString))
{
    throw new Exception("Database connection string is not set.");
}

builder.Services.AddDbContext<AppDbContext>(options => options.UseNpgsql(connectionString));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}
app.UseFileServer();
app.MapGet(
    "/",
    async context =>
    {
        var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "index.html");
        var content = await File.ReadAllTextAsync(filePath);
        context.Response.ContentType = "text/html";
        await context.Response.WriteAsync(content);
    }
);

app.MapGet(
    "/{id}",
    async (HttpContext context, AppDbContext db, string id) =>
    {
        if (!Guid.TryParse(id, out var guid))
        {
            var errorFilePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "error.html"
            );
            var errorContent = await File.ReadAllTextAsync(errorFilePath);

            return Results.Content(errorContent, "text/html");
        }

        var paste = await db.Pastes.FirstOrDefaultAsync(p => p.Guid == guid);
        if (paste is not null)
        {
            var pasteFilePath = Path.Combine(
                Directory.GetCurrentDirectory(),
                "wwwroot",
                "paste.html"
            );

            var pasteContent = await File.ReadAllTextAsync(pasteFilePath);
            pasteContent = pasteContent.Replace("{{content}}", paste.Content);
            return Results.Content(pasteContent, "text/html");
        }

        return Results.NotFound();
    }
);

app.MapPost(
    "/",
    async (AppDbContext db, PasteRequest request) =>
    {
        if (string.IsNullOrWhiteSpace(request.Content))
        {
            return Results.BadRequest("Content cannot be empty.");
        }

        var paste = new Paste { Content = request.Content };
        db.Pastes.Add(paste);
        await db.SaveChangesAsync();

        return Results.Redirect($"/{paste.Guid}");
    }
);

app.Run();

public record PasteRequest(string Content);
