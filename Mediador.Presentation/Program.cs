using System.Text.Json.Serialization;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Services;
using Mediador.Infrastructure.Clientes;
using Mediador.Infrastructure.Data;
using Mediador.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services
    .AddControllers()
    .AddJsonOptions(options =>
    {
        options.JsonSerializerOptions.PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase;
        options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    });

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<MediadorDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MediadorDev")));

builder.Services.AddScoped<IEnvioRepository, EnvioRepository>();
builder.Services.AddScoped<ITransportistaCotizableRepository, TransportistaCotizableRepository>();
builder.Services.AddScoped<ICotizacionRepository, CotizacionRepository>();
builder.Services.AddScoped<ICotizacionMediadorService, CotizacionMediadorService>();
builder.Services.AddHttpClient();
builder.Services.AddScoped<ISimuladorTransportistaClient, SimuladorTransportistaClient>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<IRutaTarifaEventoRepository, RutaTarifaEventoRepository>();
builder.Services.AddScoped<IEventoMediadorService, EventoMediadorService>();
builder.Services.AddScoped<IEnvioConsultaService, EnvioConsultaService>();
builder.Services.AddScoped<IConfirmacionRepository, ConfirmacionRepository>();
builder.Services.AddScoped<IConfirmacionMediadorService, ConfirmacionMediadorService>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
