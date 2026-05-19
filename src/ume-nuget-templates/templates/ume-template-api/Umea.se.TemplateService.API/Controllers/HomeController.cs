using Microsoft.AspNetCore.Mvc;
#if (isSmall)
using Umea.se.TemplateService.API.Infrastructure;
#else
using Umea.se.TemplateService.Shared.Infrastructure;
#endif
using Umea.se.Toolkit.Controllers;

namespace Umea.se.TemplateService.API.Controllers;

[ApiController]
[Route(ApiRoutesBase.Home)]
public class HomeController(ILogger<HomeController> logger, ApplicationConfig config) : HomeControllerBase(logger, config);
