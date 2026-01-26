using E_commerce.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Qdrant.Client;
using StackExchange.Redis;
using System.IdentityModel.Tokens.Jwt;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using System.Text;
using Microsoft.IdentityModel.Tokens;




var builder = WebApplication.CreateBuilder(args);
builder.Services.AddScoped<IJwtService, JwtService>();
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,

            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
        options.Events = new JwtBearerEvents
        {
            OnMessageReceived = context =>
            {
                context.Token = context.Request.Cookies["jwt"];
                return Task.CompletedTask;
            }
        };
    });

builder.Services.AddAuthorization();


// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddDbContext<E_commerceContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("E_commerceContext") ?? throw new InvalidOperationException("Connection string 'E_commerceContext' not found.")));


// Redis for cart storage
builder.Services.AddSingleton<IConnectionMultiplexer>(
    ConnectionMultiplexer.Connect("localhost:6379,abortConnect=false,connectTimeout=2000"));

// Qdrant Cloud Client
builder.Services.AddSingleton<QdrantClient>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var url = config["Qdrant:Url"] ?? throw new InvalidOperationException("Qdrant:Url not configured");
    var apiKey = config["Qdrant:ApiKey"] ?? throw new InvalidOperationException("Qdrant:ApiKey not configured");
    return new QdrantClient(new Uri(url), apiKey);
});

// AI Services (SOLID)
builder.Services.AddHttpClient<E_commerce.Services.AI.IEmbeddingService, E_commerce.Services.AI.OllamaEmbeddingService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});
builder.Services.AddHttpClient<E_commerce.Services.AI.IChatService, E_commerce.Services.AI.GroqChatService>(client =>
{
    client.Timeout = TimeSpan.FromMinutes(5);
});

builder.Services.AddScoped<E_commerce.Services.AI.IVectorStore, E_commerce.Services.AI.QdrantVectorStore>();
builder.Services.AddScoped<E_commerce.Services.AI.ISearchOrchestrator, E_commerce.Services.AI.ProductSearchOrchestrator>();
builder.Services.AddScoped<E_commerce.Services.AI.VectorStoreSeeder>();
//builder.Services.AddScoped<E_commerce.Services.ICartService, E_commerce.Services.CartService>();
builder.Services.AddScoped<E_commerce.Services.AI.IChatContextService, E_commerce.Services.AI.ChatContextService>();
builder.Services.AddHttpContextAccessor();

var app = builder.Build();

// Data Seeding
// Data Seeding moved to Index.cshtml.cs
// using (var scope = app.Services.CreateScope())
// {
//     try 
//     {
//          // Seeding is now triggered by the application usage (Index page)
//          // passing the data directly to the seeder.
//     }
//     catch (Exception ex)
//     {
//         var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
//         logger.LogError(ex, "Failed to seed vector store.");
//     }
// }


// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}
app.UseAuthentication();
app.UseAuthorization();
app.UseHttpsRedirection();

app.UseRouting();


app.MapStaticAssets();
app.MapRazorPages()
   .WithStaticAssets();

app.Run();
