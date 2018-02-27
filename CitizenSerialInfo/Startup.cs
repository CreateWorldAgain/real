using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc.Razor;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using CitizenSerialInfo.Domains;
using CitizenSerialInfo.Models;
using CitizenSerialInfo.Models.ViewModels;
using CitizenSerialInfo.Services;
using static CitizenSerialInfo.Models.ImportFileInfo;

namespace CitizenSerialInfo
{
    public class Startup
    {
        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public IConfiguration Configuration { get; }

        // This method gets called by the runtime. Use this method to add services to the container.
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContext<AppDbContext>(option =>
                    option.UseSqlServer(Configuration.GetConnectionString("MyConnString")));

            services.Configure<AppConfigurations>(
                     Configuration.GetSection("AppConfigurations"));


            // Add application services.
            services.AddTransient<IEmailSender, EmailSender>();

            services.AddScoped<IViewRenderService, ViewRenderService>();

            services.AddIdentity<ApplicationUser, ApplicationRole>(s=>
                 {
                     s.SignIn.RequireConfirmedEmail = true;
                     s.Password.RequireDigit = false;
                     s.Password.RequiredLength = 4;
                     s.Password.RequireNonAlphanumeric = false;
                     s.Password.RequireUppercase = false;
                     s.Password.RequireLowercase = false;
                 })
                .AddEntityFrameworkStores<AppDbContext>()
                .AddDefaultTokenProviders();

            services.ConfigureApplicationCookie(config => config.ExpireTimeSpan = TimeSpan.FromDays(36500));

            #region Localization 
            //Adding Localisation to an ASP.NET Core application
            //https://andrewlock.net/adding-localisation-to-an-asp-net-core-application/
            //Globalization and localization
            //https://docs.microsoft.com/en-us/aspnet/core/fundamentals/localization
            services.AddLocalization(opts => { opts.ResourcesPath = "Resources"; });

            services.AddMvc()
                .AddViewLocalization(LanguageViewLocationExpanderFormat.Suffix, opts => { opts.ResourcesPath = "Resources"; })
                .AddDataAnnotationsLocalization();

            services.Configure<RequestLocalizationOptions>(opts =>
            {
                //https://msdn.microsoft.com/en-us/library/cc233982.aspx
                var supportedCultures = new List<CultureInfo>
                {
                    new CultureInfo("de"),
                    new CultureInfo("en")
                };

                opts.DefaultRequestCulture = new RequestCulture("en");
                // Formatting numbers, dates, etc.
                opts.SupportedCultures = supportedCultures;
                // UI strings that we have localized.
                opts.SupportedUICultures = supportedCultures;

            });
            #endregion
            services.Configure<EmailSettings>(Configuration.GetSection("EmailSettings"));
            services.AddMvc().AddJsonOptions(options => options.SerializerSettings.ContractResolver = new Newtonsoft.Json.Serialization.DefaultContractResolver());
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app, IHostingEnvironment env, ILoggerFactory loggerFactory)
        {
            try
            {                
                loggerFactory.AddConsole(Configuration.GetSection("Logging"));
                loggerFactory.AddDebug();
                loggerFactory.AddFile("logs/webapp-{Date}.txt");

                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                    app.UseBrowserLink();
                }
                else
                {
                    app.UseExceptionHandler("/Home/Error");
                }

                app.UseStaticFiles();

                app.UseAuthentication();

                // локализация
                var options = app.ApplicationServices.GetService<IOptions<RequestLocalizationOptions>>();
                app.UseRequestLocalization(options.Value);

                // Автоматическая миграция
                using (var scope = app.ApplicationServices.GetService<IServiceScopeFactory>().CreateScope())
                {
                    // основной контекст данных
                    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();

                    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
                    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<ApplicationRole>>();
                    var logger = loggerFactory.CreateLogger("Initialize");

                    if (!dbContext.AllMigrationsApplied())
                    {
                        dbContext.Database.Migrate();                        
                    }
                    dbContext.EnsureSeedData(userManager, roleManager, logger).Wait();
                }

                app.UseMvc(routes =>
                {
                    routes.MapRoute(
                        name: "default",
                        template: "{controller=Home}/{action=Index}/{id?}");
                });
            }
            catch(Exception ex)
            {
                throw ex;
            }

            
        }



    }
}
