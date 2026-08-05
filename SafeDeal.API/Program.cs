using SafeDeal.API.Extensions;
using SafeDeal.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddApiServices(builder.Configuration);
builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseApiMiddlewares();
app.MapControllers();

app.Run();

public partial class Program { }    