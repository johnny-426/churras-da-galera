using CdG.Models;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace CdG.Data
{
    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(
            DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        public DbSet<Evento> Eventos { get; set; }

        public DbSet<VinculoEvento> VinculosEvento { get; set; }

        public DbSet<Convite> Convites { get; set; }

        public DbSet<Notificacao> Notificacoes { get; set; }

        public DbSet<Despesa> Despesas => Set<Despesa>();
        public DbSet<CotaEvento> CotasEvento => Set<CotaEvento>();
        public DbSet<Acerto> Acertos => Set<Acerto>();

        protected override void OnModelCreating(ModelBuilder builder)
        {
            base.OnModelCreating(builder);
            builder.Entity<Despesa>().HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Despesa>().HasOne(x => x.Pagador).WithMany().HasForeignKey(x => x.PagadorId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<CotaEvento>().HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<CotaEvento>().HasOne(x => x.Usuario).WithMany().HasForeignKey(x => x.UsuarioId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<CotaEvento>().HasIndex(x => new { x.EventoId, x.UsuarioId }).IsUnique();
            builder.Entity<Acerto>().HasOne(x => x.Evento).WithMany().HasForeignKey(x => x.EventoId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Acerto>().HasOne(x => x.De).WithMany().HasForeignKey(x => x.DeId).OnDelete(DeleteBehavior.Restrict);
            builder.Entity<Acerto>().HasOne(x => x.Para).WithMany().HasForeignKey(x => x.ParaId).OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Evento>()
                .HasOne(evento => evento.Criador)
                .WithMany()
                .HasForeignKey(evento => evento.CriadorId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VinculoEvento>()
                .HasOne(vinculo => vinculo.Evento)
                .WithMany()
                .HasForeignKey(vinculo => vinculo.EventoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VinculoEvento>()
                .HasOne(vinculo => vinculo.Usuario)
                .WithMany()
                .HasForeignKey(vinculo => vinculo.UsuarioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<VinculoEvento>()
                .HasIndex(vinculo => new
                {
                    vinculo.EventoId,
                    vinculo.UsuarioId
                })
                .IsUnique();

            builder.Entity<Convite>()
                .HasOne(convite => convite.Evento)
                .WithMany()
                .HasForeignKey(convite => convite.EventoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Convite>()
                .HasOne(convite => convite.Destinatario)
                .WithMany()
                .HasForeignKey(convite => convite.DestinatarioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Convite>()
                .HasIndex(convite => new
                {
                    convite.EventoId,
                    convite.DestinatarioId
                })
                .IsUnique();

            builder.Entity<Notificacao>()
                .HasOne(notificacao => notificacao.Destinatario)
                .WithMany()
                .HasForeignKey(notificacao => notificacao.DestinatarioId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notificacao>()
                .HasOne(notificacao => notificacao.Evento)
                .WithMany()
                .HasForeignKey(notificacao => notificacao.EventoId)
                .OnDelete(DeleteBehavior.Restrict);

            builder.Entity<Notificacao>()
                .HasIndex(notificacao => new
                {
                    notificacao.DestinatarioId,
                    notificacao.CriadaEm
                });
        }
    }
}