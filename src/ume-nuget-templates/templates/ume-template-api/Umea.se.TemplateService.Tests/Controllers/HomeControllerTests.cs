using System.Net;
using Umea.se.TestToolkit.TestInfrastructure;

namespace Umea.se.TemplateService.Tests.Controllers;

#if (isCloud)
public class HomeControllerTests : ControllerTestCloud<WebAppFactory, Program, HttpClientNames>
#elif (isOnPrem)
public class HomeControllerTests : ControllerTestOnPrem<WebAppFactory, Program, HttpClientNames>
#endif
{
    [Fact]
    public async Task Ping_ReturnsOkWithPong()
    {
        HttpResponseMessage response = await Client.GetAsync("/api/v1.0/home/ping");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        string content = await response.Content.ReadAsStringAsync();
        Assert.Equal("pong", content);
    }
}
