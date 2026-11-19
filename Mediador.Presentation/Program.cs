using System.Text.Json.Serialization;
using Mediador.ApplicationCore.Interfaces;
using Mediador.ApplicationCore.Services;
using Mediador.ApplicationCore.Utilidades;
using Mediador.Infrastructure.Clientes;
using Mediador.Infrastructure.Data;
using Mediador.Infrastructure.Repositories;
using Mediador.Presentation.Middlewares;
using Mediador.Presentation.Services;
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

builder.Services.AddOptions<ParametrosOptions>()
    .BindConfiguration(ParametrosOptions.Seccion)
    .Validate(p => p.t_precio_eett > TimeSpan.Zero, "t_precio_eett debe ser mayor a TimeSpan.Zero.")
    .Validate(p => p.t_registro_eett > TimeSpan.Zero, "t_registro_eett debe ser mayor a TimeSpan.Zero.")
    .Validate(p => p.t_vigencia_cotizacion > TimeSpan.Zero, "t_vigencia_cotizacion debe ser mayor a TimeSpan.Zero.")
    .Validate(p => p.t_recuperacion > TimeSpan.Zero, "t_recuperacion debe ser mayor a TimeSpan.Zero.")
    .Validate(p => p.t_mensajes > TimeSpan.Zero, "t_mensajes debe ser mayor a TimeSpan.Zero.")
    .Validate(p => p.r_esperas_sincronas is not null && p.r_esperas_sincronas.Length > 0 && p.r_esperas_sincronas.All(t => t > TimeSpan.Zero), "r_esperas_sincronas no puede ser nulo, debe tener al menos un elemento y todos sus tiempos deben ser mayores a TimeSpan.Zero.")
    .ValidateOnStart();

builder.Services.AddDbContext<MediadorDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("MediadorDev")));

builder.Services.AddScoped<IEnvioRepository, EnvioRepository>();
builder.Services.AddScoped<ITransportistaCotizableRepository, TransportistaCotizableRepository>();
builder.Services.AddScoped<ICotizacionRepository, CotizacionRepository>();
builder.Services.AddScoped<ICotizacionMediadorService, CotizacionMediadorService>();
builder.Services.AddHttpClient("", _ => { }).ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
{
    ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
});
builder.Services.AddScoped<ISimuladorTransportistaClient, SimuladorTransportistaClient>();
builder.Services.AddScoped<IEventoRepository, EventoRepository>();
builder.Services.AddScoped<IRutaTarifaEventoRepository, RutaTarifaEventoRepository>();
builder.Services.AddScoped<IEventoMediadorService, EventoMediadorService>();
builder.Services.AddScoped<IEnvioConsultaService, EnvioConsultaService>();
builder.Services.AddScoped<IConfirmacionRepository, ConfirmacionRepository>();
builder.Services.AddScoped<IConfirmacionMediadorService, ConfirmacionMediadorService>();
builder.Services.AddScoped<ICapacidadesTransportistaRepository, CapacidadesTransportistaRepository>();
builder.Services.AddScoped<ICapacidadesTransportistaService, CapacidadesTransportistaService>();

builder.Services.AddSingleton<INotificadorHuecoSecuencia, NotificadorHuecoSecuencia>();
builder.Services.AddHostedService<RecuperacionEventosBackgroundService>();
builder.Services.AddHostedService<SincronizacionCapacidadesBackgroundService>();

var app = builder.Build();

app.UseMiddleware<ManejadorExcepcionesMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthorization();
app.MapControllers();

app.Run();
