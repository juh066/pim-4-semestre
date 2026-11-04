using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuroraGaleria.Tests;

public class ApiFlowTests : IClassFixture<AuroraWebFactory>
{
    private readonly AuroraWebFactory _factory;
    public ApiFlowTests(AuroraWebFactory factory) => _factory = factory;

    [Fact]
    public async Task PublicHomeLoadsFromDatabase()
    {
        using var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Caneca Aurora", html);
        Assert.Contains("vagas", html);
        Assert.DoesNotContain("ticketSession\" required disabled", html);
    }

    [Fact]
    public async Task VisitorCannotOpenAdministrativeEndpoints()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        await RegisterAndLogin(client, $"visitor-{Guid.NewGuid():N}@example.test", "12122222222");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/user-actions/analytics")).StatusCode);
    }

    [Fact]
    public async Task RegistrationLoginCartHistoryAndLogoutWorkWithoutTrustingPostedPrice()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        await RegisterAndLogin(client, $"flow-{Guid.NewGuid():N}@example.test", "16899535009");

        using var add = await SendJson(client, HttpMethod.Post, "/cart/items", new { productId = 1, quantity = 1, requestId = Guid.NewGuid().ToString(), productName = "Falso", unitPrice = 0.01m });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var cart = await add.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(49.90m, cart.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal());

        using var checkout = await SendJson(client, HttpMethod.Post, "/cart/checkout", new { requestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var history = await client.GetFromJsonAsync<JsonElement>("/user-actions/purchases");
        Assert.Equal(49.90m, history[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal("Simulação acadêmica confirmada", history[0].GetProperty("situation").GetString());

        using var logout = await SendJson(client, HttpMethod.Post, "/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    private static async Task RegisterAndLogin(HttpClient client, string email, string cpf)
    {
        using var registration = await SendJson(client, HttpMethod.Post, "/auth/register", new { name = "Pessoa Teste", cpf, email, password = "senha-ficticia-123" });
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        using var login = await SendJson(client, HttpMethod.Post, "/auth/login", new { email, password = "senha-ficticia-123" });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Visitor", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
    }

    private static async Task<HttpResponseMessage> SendJson(HttpClient client, HttpMethod method, string url, object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
}
