// Copyright (c) Brock Allen & Dominick Baier. All rights reserved.
// Licensed under the Apache License, Version 2.0. See LICENSE in the project root for license information.


using System;
using System.Linq;
using System.Reflection;
using BotDetect.Web;
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
using Microsoft.AspNetCore.Server.Kestrel.Core;
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
        private static string _clientUri,_ndwhClientUri;
        private static string _redirectUris,_ndwhRedirectUris;
        private static string _adhocRedirectUris,_ndwhAdhocRedirectUris;
        private static string _postLogoutRedirectUris,_ndwhPostLogoutRedirectUris;
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

            _ndwhClientUri= Configuration.GetSection("NdwhClientUri").Value;
            _ndwhRedirectUris = Configuration.GetSection("NdwhRedirectUris").Value;
            _ndwhAdhocRedirectUris = Configuration.GetSection("NdwhAdhocRedirectUris").Value;
            _ndwhPostLogoutRedirectUris = Configuration.GetSection("NdwhPostLogoutRedirectUris").Value;

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

            services.AddMvc();
            services.AddMemoryCache();
            services.AddSession(options =>
            {
                options.IdleTimeout = TimeSpan.FromMinutes(20);
                options.Cookie.IsEssential = true;
            });

            var emailConfig = Configuration
                .GetSection("EmailConfiguration")
                .Get<EmailConfiguration>();
            services.AddSingleton(emailConfig);
            services.AddScoped<IEmailSender, EmailSender>();

            services.ConfigureNonBreakingSameSiteCookies();
            services.AddAuthentication();

            services.Configure<KestrelServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });

            services.Configure<IISServerOptions>(options =>
            {
                options.AllowSynchronousIO = true;
            });
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

            app.UseSession();
            app.UseCaptcha(Configuration);

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
                    else if (client.ClientId == "nascop.spa")
                    {
                        client.ClientUri = _ndwhClientUri;
                        client.RedirectUris.Add(_ndwhRedirectUris);
                        client.PostLogoutRedirectUris.Add(_ndwhPostLogoutRedirectUris);
                        client.AllowedCorsOrigins.Add(_ndwhClientUri);
                    }
                    else if (client.ClientId == "nascop.adhoc-client")
                    {
                        client.RedirectUris.Add(_ndwhAdhocRedirectUris);
                    }

                    var isClientExists = configDbContext.Clients.Any(x => x.ClientId == client.ClientId);
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
