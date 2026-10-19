using CdG.Data;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.HttpOverrides;
using System.Net;
using CdG.Services;
using CdG.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CdG
{
    public class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Lê a conexão configurada nos segredos do usuário.
            var connectionString = builder.Configuration
                .GetConnectionString("DefaultConnection")
                ?? throw new InvalidOperationException(
                    "A conexão DefaultConnection não foi configurada.");

            // Configura o acesso ao PostgreSQL.
            builder.Services.AddDbContext<ApplicationDbContext>(options =>
                options.UseNpgsql(connectionString));

            // Configura usuários, senhas e páginas de autenticação.
            builder.Services
                .AddDefaultIdentity<ApplicationUser>(options =>
                {
                    // Na internet, confirme o endereço antes do primeiro acesso.
                    options.SignIn.RequireConfirmedAccount = !builder.Environment.IsDevelopment();
                    options.User.RequireUniqueEmail = true;
                })
                .AddEntityFrameworkStores<ApplicationDbContext>();

            builder.Services.AddRazorPages();
            builder.Services.AddTransient<IEmailSender, EmailSender>();
            var protection = builder.Services.AddDataProtection().SetApplicationName("CdG");
            var keyPath = builder.Configuration["DataProtection:KeysPath"];
            if (!string.IsNullOrWhiteSpace(keyPath)) protection.PersistKeysToFileSystem(new DirectoryInfo(keyPath));
            builder.Services.Configure<ForwardedHeadersOptions>(options =>
            {
                options.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
                var proxy = builder.Configuration["ReverseProxy:KnownProxy"];
                if (!string.IsNullOrWhiteSpace(proxy)) options.KnownProxies.Add(IPAddress.Parse(proxy));
            });

            // Configura o serviço de notificações.
            builder.Services.AddScoped<NotificacaoService>();
            builder.Services.AddScoped<ParticipacaoService>();
            builder.Services.AddScoped<FinanceiroService>();
            var app = builder.Build();

            app.UseForwardedHeaders();
            if (args.Contains("--migrate"))
            {
                using var scope = app.Services.CreateScope();
                scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.Migrate();
                return;
            }
            if (!app.Environment.IsDevelopment())
            {
                app.UseExceptionHandler("/Error");
                app.UseHsts();
            }

            app.UseHttpsRedirection();

            app.UseRequestLocalization(new RequestLocalizationOptions()
                .SetDefaultCulture("pt-BR").AddSupportedCultures("pt-BR").AddSupportedUICultures("pt-BR"));
            app.UseRouting();

            // Identifica o usuário conectado.
            app.UseAuthentication();

            // Verifica suas permissões de acesso.
            app.UseAuthorization();

            app.MapStaticAssets();

            app.MapRazorPages()
                .WithStaticAssets();

            app.Run();
        }
    }
}
