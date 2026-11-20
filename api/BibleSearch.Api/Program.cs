
using MongoDB.Driver;
using BibleSearch.Api.Services;

var builder = WebApplication.CreateBuilder(args);

// MongoDB configuration
var mongoConnectionString =
    builder.Configuration["MongoDB:ConnectionString"]
    ?? throw new InvalidOperationException(
        "MongoDB connection string is missing.");

var mongoDatabaseName =
    builder.Configuration["MongoDB:DatabaseName"]
    ?? throw new InvalidOperationException(
        "MongoDB database name is missing.");

builder.Services.AddSingleton<IMongoClient>(
    new MongoClient(mongoConnectionString));

builder.Services.AddSingleton<IMongoDatabase>(serviceProvider =>
{
    var client = serviceProvider.GetRequiredService<IMongoClient>();
    return client.GetDatabase(mongoDatabaseName);
});

// Application services
builder.Services.AddSingleton<MongoDbService>();

builder.Services.AddHttpClient<EmbeddingService>();

builder.Services.AddScoped<BibleImportService>();
builder.Services.AddScoped<ChunkImportService>();
builder.Services.AddScoped<BibleTextParserService>();

// CORS configuration
builder.Services.AddCors(options =>
{
    options.AddPolicy("ReactFrontend", policy =>
    {
        policy
            .WithOrigins("http://localhost:5173")
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});

// Controllers and Swagger
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// HTTP request pipeline
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("ReactFrontend");

app.UseAuthorization();

app.MapControllers();

app.Run();
