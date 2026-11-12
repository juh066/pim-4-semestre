using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace AuroraGaleria.Tests;

public class AdminRegistrationTests
{
    private const string Password = "senha-ficticia-123";

    [Fact]
    public async Task CommonRegistrationRemainsVisitorWhenDemoIsEnabled()
    {
        using var factory = EnabledFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var registration = await Register(client, "visitante@example.test");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Equal("Visitor", (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        using var login = await Login(client, "visitante@example.test");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Visitor", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task ValidDemoCodeCreatesAdministratorWhoCanAccessManagement()
    {
        using var factory = EnabledFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var home = await client.GetStringAsync("/");
        Assert.Contains("Cadastrar como administrador", home);
        Assert.Contains("Utilize o código 123", home);

        using var registration = await Register(
            client, "administrador@example.test", true, "123");
        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Equal("Administrator", (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        using var login = await Login(client, "administrador@example.test");
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal("Administrator", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        using var administration = await client.GetAsync("/admin");
        Assert.Equal(HttpStatusCode.OK, administration.StatusCode);
        var administrationHtml = await administration.Content.ReadAsStringAsync();
        Assert.Contains("href=\"/#comprar-ingressos\"", administrationHtml);
        Assert.Contains("href=\"/#agendar-visita\"", administrationHtml);
        Assert.Contains("href=\"/#lojinha\"", administrationHtml);
    }

    [Fact]
    public async Task CancelledPurchaseLeavesSalesAnalyticsButRemainsInHistory()
    {
        using var factory = EnabledFactory();
        using var visitor = factory.CreateClient(new() { AllowAutoRedirect = false });
        using var administrator = factory.CreateClient(new() { AllowAutoRedirect = false });
        var suffix = Guid.NewGuid().ToString("N");
        var visitorEmail = $"analytics-visitor-{suffix}@example.test";
        var administratorEmail = $"analytics-admin-{suffix}@example.test";

        using var visitorRegistration = await Register(visitor, visitorEmail);
        Assert.Equal(HttpStatusCode.OK, visitorRegistration.StatusCode);
        using var visitorLogin = await Login(visitor, visitorEmail);
        Assert.Equal(HttpStatusCode.OK, visitorLogin.StatusCode);

        using var administratorRegistration = await Register(administrator, administratorEmail, true, "123");
        Assert.Equal(HttpStatusCode.OK, administratorRegistration.StatusCode);
        using var administratorLogin = await Login(administrator, administratorEmail);
        Assert.Equal(HttpStatusCode.OK, administratorLogin.StatusCode);

        using var add = await SendJson(visitor, HttpMethod.Post, "/cart/items", new
        {
            productId = 1, quantity = 2, requestId = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var cart = await add.Content.ReadFromJsonAsync<JsonElement>();
        var cartItem = cart.GetProperty("items")[0];
        var productName = cartItem.GetProperty("productName").GetString();
        var expectedQuantity = cartItem.GetProperty("quantity").GetInt32();
        var expectedRevenue = cartItem.GetProperty("subtotal").GetDecimal();

        using var checkout = await SendJson(visitor, HttpMethod.Post, "/cart/checkout", new
        {
            requestId = Guid.NewGuid().ToString("N")
        });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var orderId = (await checkout.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("orderId").GetInt32();

        var analyticsBeforeCancellation = await administrator.GetFromJsonAsync<JsonElement>("/user-actions/analytics");
        var saleBeforeCancellation = analyticsBeforeCancellation.GetProperty("vendasPorProduto").EnumerateArray()
            .Single(item => item.GetProperty("produto").GetString() == productName);
        Assert.Equal(expectedQuantity, saleBeforeCancellation.GetProperty("quantidade").GetInt32());
        Assert.Equal(expectedRevenue, saleBeforeCancellation.GetProperty("receita").GetDecimal());

        using var cancellation = await SendJson(visitor, HttpMethod.Delete, $"/user-actions/purchases/{orderId}", new { });
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);

        var analyticsAfterCancellation = await administrator.GetFromJsonAsync<JsonElement>("/user-actions/analytics");
        Assert.DoesNotContain(analyticsAfterCancellation.GetProperty("vendasPorProduto").EnumerateArray(),
            item => item.GetProperty("produto").GetString() == productName);

        var history = await visitor.GetFromJsonAsync<JsonElement>("/user-actions/purchases");
        var cancelledItem = history.EnumerateArray().Single(item => item.GetProperty("orderId").GetInt32() == orderId);
        Assert.Equal("Cancelada", cancelledItem.GetProperty("status").GetString());
        Assert.Equal(expectedQuantity, cancelledItem.GetProperty("quantity").GetInt32());
        Assert.Equal(expectedRevenue, cancelledItem.GetProperty("totalPrice").GetDecimal());
    }

    [Theory]
    [InlineData("")]
    [InlineData("codigo-incorreto")]
    public async Task EmptyOrIncorrectCodeDoesNotCreateAccount(string code)
    {
        using var factory = EnabledFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var rejected = await Register(
            client, "tentativa-admin@example.test", true, code);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("Código de acesso administrativo inválido.", await rejected.Content.ReadAsStringAsync());

        using var accepted = await Register(
            client, "tentativa-admin@example.test", true, "123");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
    }

    [Fact]
    public async Task PostedRoleCannotPromoteACommonRegistration()
    {
        using var factory = EnabledFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var registration = await SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Visitante Direto",
            email = "direto@example.test",
            password = Password,
            role = "Administrator",
            enableAdminRegistration = true
        });

        Assert.Equal(HttpStatusCode.OK, registration.StatusCode);
        Assert.Equal("Visitor", (await registration.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());

        using var login = await Login(client, "direto@example.test");
        Assert.Equal("Visitor", (await login.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin")).StatusCode);
    }

    [Fact]
    public async Task DisabledDemoHidesOptionAndRejectsAdministrativeRegistration()
    {
        using var factory = new AuroraWebFactory(false, "123");
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var home = await client.GetStringAsync("/");
        Assert.DoesNotContain("Cadastrar como administrador", home);

        using var rejected = await Register(
            client, "desativado@example.test", true, "123");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        Assert.Contains("não está disponível", await rejected.Content.ReadAsStringAsync());

        using var common = await Register(client, "desativado@example.test");
        Assert.Equal(HttpStatusCode.OK, common.StatusCode);
        Assert.Equal("Visitor", (await common.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("role").GetString());
    }

    [Fact]
    public async Task AdministrativeRegistrationKeepsEmailAndDuplicateProtections()
    {
        using var factory = EnabledFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        using var invalidEmail = await Register(
            client, "admin@incompleto", true, "123");
        Assert.Equal(HttpStatusCode.BadRequest, invalidEmail.StatusCode);

        using var first = await Register(
            client, "admin.unico@example.test", true, "123");
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var duplicate = await Register(
            client, "  ADMIN.UNICO@example.test  ", true, "123");
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
    }

    private static AuroraWebFactory EnabledFactory() => new(true, "123");

    private static Task<HttpResponseMessage> Register(
        HttpClient client,
        string email,
        bool registerAsAdministrator = false,
        string? adminAccessCode = null) =>
        SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Pessoa Teste",
            email,
            password = Password,
            registerAsAdministrator,
            adminAccessCode
        });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email) =>
        SendJson(client, HttpMethod.Post, "/auth/login", new { email, password = Password });

    private static async Task<HttpResponseMessage> SendJson(
        HttpClient client,
        HttpMethod method,
        string url,
        object body)
    {
        var csrf = await client.GetFromJsonAsync<JsonElement>("/auth/csrf");
        using var request = new HttpRequestMessage(method, url) { Content = JsonContent.Create(body) };
        request.Headers.Add("X-CSRF-TOKEN", csrf.GetProperty("token").GetString());
        return await client.SendAsync(request);
    }
}
