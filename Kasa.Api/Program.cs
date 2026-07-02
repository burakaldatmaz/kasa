using System.Text.Json.Serialization;
using Kasa.Api.Data;
using Kasa.Api.Endpoints;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddOpenApi();

builder.Services.AddDbContext<KasaDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Kasa") ?? "Data Source=kasa.db"));

builder.Services.ConfigureHttpJsonOptions(options =>
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// "Bugün" kavramı (filo eksik gün sayımı) testte sabitlenebilsin diye DI üzerinden verilir.
builder.Services.AddSingleton(TimeProvider.System);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.MapCategoryEndpoints();
app.MapTransactionEndpoints();
app.MapReportEndpoints();
app.MapFleetEndpoints();

app.Run();

// WebApplicationFactory<Program> erişimi için (Kasa.Tests).
public partial class Program { }
