#if (hasDatabase)
using Microsoft.EntityFrameworkCore;
#endif
using Umea.se.TemplateService.API;
#if (hasDatabase)
using Umea.se.TemplateService.Database;
#endif
#if (isSmall)
using Umea.se.TemplateService.API.Infrastructure;
#else
using Umea.se.TemplateService.Logic;
using Umea.se.TemplateService.ServiceAccess;
using Umea.se.TemplateService.Shared;
using Umea.se.TemplateService.Shared.Infrastructure;
#endif
using Umea.se.Toolkit.EntryPoints;
using Umea.se.Toolkit.Filters;

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

ApplicationConfig config = new(builder.Configuration, typeof(Program).Assembly);

#if (isCloud)
if (!config.SuppressKeyVaultConfigs)
{
    builder.Logging.UseDefaultLoggers(config);
}
#elif (isOnPrem)
builder.Logging.UseDefaultLoggers(config);
#endif

builder.Services
    .AddApplicationConfig(config)
    .AddApiDependencies()
#if (!isSmall)
    .AddLogicDependencies()
    .AddServiceAccessDependencies()
    .AddSharedDependencies()
#endif
#if (hasDatabase)
    .AddDatabaseDependencies()
    .AddDbContext<TemplateVarDatabaseName>(options => options.UseSqlServer(config.TemplateVarDatabaseNameConnectionString))
#endif
    ;

#if (!isOnPrem)
builder.Services.AddAllowedOriginsCorsPolicy(config.AllowedOrigins);
#endif

builder.Services.AddControllers(options =>
{
    options.Filters.Add<HttpResponseValidationFilter>();
    options.Filters.Add<HttpResponseExceptionFilter>();
});
builder.Services.AddDefaultSwagger(config);

WebApplication app = builder.Build();

app.UseDefaultSwagger(config);
app.UseHttpsRedirection();
#if (!isOnPrem)
app.UseAllowedOriginsCorsPolicy();
#endif
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

public partial class Program;
