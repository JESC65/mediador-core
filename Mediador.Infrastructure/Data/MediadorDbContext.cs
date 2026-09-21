using Microsoft.EntityFrameworkCore;
using Mediador.Infrastructure.Models;

namespace Mediador.Infrastructure.Data;

/// <summary>
/// El esquema MED ya existe (lo crea 01_esquema.sql, a mano, database-first).
/// Este DbContext solo mapea encima de esas tablas — no hay migraciones de
/// EF Core en este proyecto, y no deben agregarse sin avisar, porque una
/// migración intentaría recrear tablas que ya están creadas por script.
///
/// Solo mapeo acá las ocho tablas que necesita la cotización de hoy
/// (Bloque 2). Faltan Courier, EnvioPersona, PrecioVenta, EventoRecibido y
/// EnvioDocumento — se agregan cuando el bloque que las use (confirmación,
/// eventos, etc.) las necesite.
/// </summary>
public class MediadorDbContext : DbContext
{
    public MediadorDbContext(DbContextOptions<MediadorDbContext> options) : base(options)
    {
    }

    public DbSet<Transportista> Transportistas => Set<Transportista>();
    public DbSet<Convenio> Convenios => Set<Convenio>();
    public DbSet<Ruta> Rutas => Set<Ruta>();
    public DbSet<TarifaProveedor> TarifasProveedor => Set<TarifaProveedor>();
    public DbSet<Envio> Envios => Set<Envio>();
    public DbSet<EnvioPaquete> EnvioPaquetes => Set<EnvioPaquete>();
    public DbSet<EnvioEstado> EnvioEstados => Set<EnvioEstado>();
    public DbSet<Cotizacion> Cotizaciones => Set<Cotizacion>();
    public DbSet<EventoRecibido> EventosRecibidos => Set<EventoRecibido>();
    public DbSet<EnvioPersona> EnvioPersonas => Set<EnvioPersona>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Transportista>(e =>
        {
            e.ToTable("Transportista", "MED");
            e.HasKey(x => x.IdTransportista);
            e.Property(x => x.IdTransportista).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Convenio>(e =>
        {
            e.ToTable("Convenio", "MED");
            e.HasKey(x => x.IdConvenio);
            e.Property(x => x.IdConvenio).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Ruta>(e =>
        {
            e.ToTable("Ruta", "MED");
            e.HasKey(x => x.IdRuta);
            e.Property(x => x.IdRuta).ValueGeneratedOnAdd();
            e.Property(x => x.PesoMaximoKg).HasPrecision(8, 2);
        });

        modelBuilder.Entity<TarifaProveedor>(e =>
        {
            e.ToTable("TarifaProveedor", "MED");
            e.HasKey(x => x.IdTarifaProveedor);
            e.Property(x => x.IdTarifaProveedor).ValueGeneratedOnAdd();
            e.Property(x => x.PesoDesdeKg).HasPrecision(8, 2);
            e.Property(x => x.PesoHastaKg).HasPrecision(8, 2);
            e.Property(x => x.Precio).HasPrecision(10, 2);
            e.Property(x => x.PrecioMinimo).HasPrecision(10, 2);
        });

        modelBuilder.Entity<Envio>(e =>
        {
            e.ToTable("Envio", "MED");
            e.HasKey(x => x.IdEnvio);
            e.Property(x => x.IdEnvio).ValueGeneratedOnAdd();
            e.Property(x => x.PesoTotalKg).HasPrecision(8, 2);
        });

        modelBuilder.Entity<EnvioPaquete>(e =>
        {
            e.ToTable("EnvioPaquete", "MED");
            e.HasKey(x => x.IdEnvioPaquete);
            e.Property(x => x.IdEnvioPaquete).ValueGeneratedOnAdd();
            e.Property(x => x.PesoKg).HasPrecision(8, 2);
            e.Property(x => x.LargoCm).HasPrecision(8, 2);
            e.Property(x => x.AnchoCm).HasPrecision(8, 2);
            e.Property(x => x.AltoCm).HasPrecision(8, 2);
            e.Property(x => x.ValorDeclarado).HasPrecision(10, 2);
        });

        modelBuilder.Entity<EnvioEstado>(e =>
        {
            e.ToTable("EnvioEstado", "MED");
            e.HasKey(x => x.IdEnvioEstado);
            e.Property(x => x.IdEnvioEstado).ValueGeneratedOnAdd();
        });

        modelBuilder.Entity<Cotizacion>(e =>
        {
            e.ToTable("Cotizacion", "MED");
            e.HasKey(x => x.IdCotizacion);
            e.Property(x => x.IdCotizacion).ValueGeneratedOnAdd();
            e.Property(x => x.PrecioProveedor).HasPrecision(10, 2);
        });
        modelBuilder.Entity<EventoRecibido>(e =>
        {
            e.ToTable("EventoRecibido", "MED");
            e.HasKey(x => x.IdEventoRecibido);
            e.Property(x => x.IdEventoRecibido).ValueGeneratedOnAdd();
        });
    }
}
