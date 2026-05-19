using System.Reflection;
#if (!isSmall)
using Microsoft.Extensions.Configuration;
#endif
using Umea.se.Toolkit.Configuration;

#if (isSmall)
namespace Umea.se.TemplateService.API.Infrastructure;
#else
namespace Umea.se.TemplateService.Shared.Infrastructure;
#endif

#if (isCloud)
public class ApplicationConfig(IConfiguration configuration, Assembly? entryAssembly = null) : ApplicationConfigCloudBase(configuration, entryAssembly)
#elif (isOnPrem)
public class ApplicationConfig(IConfiguration configuration, Assembly? entryAssembly = null) : ApplicationConfigOnPremBase(configuration, entryAssembly)
#endif
{
#if (hasDatabase)
    public string TemplateVarDatabaseNameConnectionString => GetValue("ConnectionStrings:TemplateVarDatabaseName");
#endif
}
