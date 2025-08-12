using rinha_2025_rafael.CrossCutting;
using rinha_2025_rafael.Endpoints;

var builder = WebApplication.CreateSlimBuilder(args);

builder.Services.AddInfrastructure(builder.Configuration);

var app = builder.Build();

app.MapPaymentsEndpoints();

app.Run();