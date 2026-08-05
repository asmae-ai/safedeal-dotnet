using SafeDeal.API.Extensions;
using SafeDeal.Infrastructure;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference(options =>
    {
        options.Title = "SafeDeal API";
        options.Theme = ScalarTheme.Purple;
    });
}

app.UseApiMiddlewares();
app.MapControllers();

app.Run();

public partial class Program { }