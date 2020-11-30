// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System.Linq;
using System.Reflection;
using Dwh.IS4Host.Data;
using Dwh.IS4Host.Models;
using EmailService;
using IdentityServer4;
using IdentityServer4.EntityFramework.DbContexts;
using IdentityServer4.EntityFramework.Mappers;
using IdentityServer4.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Internal;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dwh.IS4Host
{
    public class Startup
    {
        public IWebHostEnvironment Environment { get; }
        public IConfiguration Configuration { get; }
        private static string _clientUri;
        private static string _redirectUris;
        private static string _adhocRedirectUris;
        private static string _postLogoutRedirectUris;
        private static string[] _allowedOrigins;

        public Startup(IWebHostEnvironment environment, IConfiguration configuration)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .AddJsonFile($"appsettings.{environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
                .AddEnvironmentVariables();

            Environment = environment;
            Configuration = builder.Build();
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddControllersWithViews();

            var connectionString = Configuration.GetConnectionString("DefaultConnection");
            _clientUri = Configuration.GetSection("ClientUri").Value;
            _redirectUris = Configuration.GetSection("RedirectUris").Value;
            _adhocRedirectUris = Configuration.GetSection("AdhocRedirectUris").Value;
            _postLogoutRedirectUris = Configuration.GetSection("PostLogoutRedirectUris").Value;

            // store assembly for migrations
            var migrationsAssembly = typeof(Startup).GetTypeInfo().Assembly.GetName().Name;

            services.AddDbContext<ApplicationDbContext>(options =>
                options.UseSqlServer(connectionString));

            services.AddIdentity<ApplicationUser, IdentityRole>(options =>
            {
                // password
                options.Password.RequiredLength = 7;
                options.Password.RequireDigit = true;
                options.Password.RequireUppercase = true;
                options.User.RequireUniqueEmail = true;
                options.SignIn.RequireConfirmedEmail = true;
            })
            .AddEntityFrameworkStores<ApplicationDbContext>()
            .AddDefaultTokenProviders();

            var builder = services.AddIdentityServer(options =>
            {
                options.Events.RaiseErrorEvents = true;
                options.Events.RaiseInformationEvents = true;
                options.Events.RaiseFailureEvents = true;
                options.Events.RaiseSuccessEvents = true;

                // see https://identityserver4.readthedocs.io/en/latest/topics/resources.html
                options.EmitStaticAudienceClaim = true;
            })
            .AddConfigurationStore(configDb =>
            {
                configDb.ConfigureDbContext = db => db.UseSqlServer(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddOperationalStore(operationDb =>
            {
                operationDb.ConfigureDbContext = db => db.UseSqlServer(connectionString,
                    sql => sql.MigrationsAssembly(migrationsAssembly));
            })
            .AddAspNetIdentity<ApplicationUser>();

            if (Environment.IsDevelopment())
            {
                // not recommended for production - you need to store your key material somewhere secure
                builder.AddDeveloperSigningCredential();
            }
            else
            {
                builder.AddCertificateFromFile(Configuration,Environment);
            }

            services.AddSwaggerGen();
            services.AddScoped<IProfileService, IdentityProfileService>();
            services.AddSingleton<IHttpContextAccessor, HttpContextAccessor>();

            var emailConfig = Configuration
                .GetSection("EmailConfiguration")
                .Get<EmailConfiguration>();
            services.AddSingleton(emailConfig);
            services.AddScoped<IEmailSender, EmailSender>();

            services.ConfigureNonBreakingSameSiteCookies();
            services.AddAuthentication();
        }

        public void Configure(IApplicationBuilder app)
        {
            //Ensure Database is seeded
            InitializeDatabase(app);

            if (Environment.IsDevelopment())
            {
                app.UseDeveloperExceptionPage();
                app.UseDatabaseErrorPage();
            }
            else
            {
                app.UseHsts();
                app.UseHttpsRedirection();
            }

            _allowedOrigins = Configuration.GetSection("AllowedOrigins").Get<string[]>();
            app.UseCors(builder => builder
                .WithOrigins(_allowedOrigins)
                .AllowAnyMethod()
                .AllowAnyHeader()
                .AllowCredentials());

            app.UseStaticFiles();

            app.UseSwagger();
            app.UseSwaggerUI(c =>
            {
                c.SwaggerEndpoint("/swagger/v1/swagger.json", "Dwh.IS4 Host V1");
            });

            app.UseRouting();
            app.UseIdentityServer();
            app.UseCookiePolicy(new CookiePolicyOptions
            {
                MinimumSameSitePolicy = SameSiteMode.Lax
            });
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints =>
            {
                endpoints.MapDefaultControllerRoute();
            });
        }

        private void InitializeDatabase(IApplicationBuilder app)
        {
            using (var serviceScope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
            {
                var applicationDbContext = serviceScope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                applicationDbContext.Database.Migrate();

                var persistedGrantDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<PersistedGrantDbContext>();
                persistedGrantDbContext.Database.Migrate();

                var configDbContext = serviceScope.ServiceProvider
                    .GetRequiredService<ConfigurationDbContext>();
                configDbContext.Database.Migrate();

                foreach (var client in Config.Clients)
                {
                    if (client.ClientId == "dwh.spa")
                    {
                        client.ClientUri = _clientUri;
                        client.RedirectUris.Add(_redirectUris);
                        client.PostLogoutRedirectUris.Add(_postLogoutRedirectUris);
                        client.AllowedCorsOrigins.Add(_clientUri);
                    } 
                    else if (client.ClientId == "adhoc-client")
                    {
                        client.RedirectUris.Add(_adhocRedirectUris);
                    }

                    var isClientExists = configDbContext.Clients.Where(x => x.ClientId == client.ClientId).Any();
                    if (!isClientExists)
                    {
                        configDbContext.Clients.Add(client.ToEntity());
                    }
                }

                configDbContext.SaveChanges();

                if (!EnumerableExtensions.Any(configDbContext.IdentityResources))
                {
                    foreach (var res in Config.IdentityResources)
                    {
                        configDbContext.IdentityResources.Add(res.ToEntity());
                    }

                    configDbContext.SaveChanges();
                }

                if (!EnumerableExtensions.Any(configDbContext.ApiScopes))
                {
                    foreach (var api in Config.ApiScopes)
                    {
                        configDbContext.ApiScopes.Add(api.ToEntity());
                    }

                    configDbContext.SaveChanges();
                }
            }
        }
    }
}
