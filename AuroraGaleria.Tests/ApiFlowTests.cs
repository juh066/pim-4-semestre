using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AuroraGaleria.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Contains("data-session-picker=\"ticket\"", html);
        Assert.Contains("data-session-picker=\"agenda\"", html);
        Assert.Contains("15 de maio a 30 de junho de 2027", html);
        Assert.Contains("\"date\":\"15/05/2027\"", html);
        Assert.Contains("\"id\":1", html);
        Assert.DoesNotContain("projeto acadêmico", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("simulação acadêmica", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("fins acadêmicos", html, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("camila@g")]
    [InlineData("camila@gmail")]
    [InlineData("camila@")]
    [InlineData("@gmail.com")]
    [InlineData("camila@@gmail.com")]
    [InlineData("camila @gmail.com")]
    [InlineData("camila@gmail..com")]
    [InlineData("camila@gmail.com.")]
    [InlineData("camila@dominio.123")]
    public async Task RegistrationRejectsInvalidEmailSentDirectly(string email)
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Camila", email, password = "senha-ficticia-123"
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("Informe um e-mail válido, como nome@dominio.com.", await response.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("camila@gmail.com")]
    [InlineData("camila@outlook.com")]
    [InlineData("camila@empresa.com.br")]
    [InlineData("camila@instituicao.org")]
    [InlineData("camila.souza+galeria@gmail.com")]
    public async Task RegistrationAcceptsSupportedEmailFormats(string email)
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        using var response = await SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Camila", email, password = "senha-ficticia-123"
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task RegistrationNormalizesEmailAndStillRejectsDuplicates()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        using var first = await SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Pessoa Um", email = "Repetido@Empresa.com.br", password = "senha-ficticia-123"
        });
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        using var duplicate = await SendJson(client, HttpMethod.Post, "/auth/register", new
        {
            name = "Pessoa Dois", email = "  repetido@empresa.com.br  ", password = "senha-ficticia-123"
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var login = await SendJson(client, HttpMethod.Post, "/auth/login", new
        {
            email = " REPETIDO@EMPRESA.COM.BR ", password = "senha-ficticia-123"
        });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task VisitorCannotOpenAdministrativeEndpoints()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        await RegisterAndLogin(client, $"visitor-{Guid.NewGuid():N}@example.test");
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/admin")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/user-actions/analytics")).StatusCode);
    }

    [Fact]
    public async Task AnonymousReservationEndpointsStillRequireAuthentication()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });

        using var ticket = await SendJson(client, HttpMethod.Post, "/user-actions/tickets", new
        {
            sessionId = 1, quantity = 1, requestId = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.Unauthorized, ticket.StatusCode);

        using var appointment = await SendJson(client, HttpMethod.Post, "/user-actions/appointments", new
        {
            sessionId = 1, requestId = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.Unauthorized, appointment.StatusCode);
    }

    [Fact]
    public async Task PublicAvailabilityCheckReflectsAFullSession()
    {
        using var factory = new AuroraWebFactory();
        using var client = factory.CreateClient(new() { AllowAutoRedirect = false });

        var available = await client.GetFromJsonAsync<JsonElement>("/sessions/1/availability");
        Assert.True(available.GetProperty("isAvailable").GetBoolean());
        Assert.True(available.GetProperty("available").GetInt32() > 0);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var session = await db.VisitSessions.SingleAsync(item => item.Id == 1);
            session.ReservedQuantity = session.Capacity;
            await db.SaveChangesAsync();
        }

        var full = await client.GetFromJsonAsync<JsonElement>("/sessions/1/availability");
        Assert.False(full.GetProperty("isAvailable").GetBoolean());
        Assert.Equal(0, full.GetProperty("available").GetInt32());
    }

    [Fact]
    public async Task RegistrationLoginCartHistoryAndLogoutWorkWithoutTrustingPostedPrice()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        await RegisterAndLogin(client, $"flow-{Guid.NewGuid():N}@example.test");

        using var add = await SendJson(client, HttpMethod.Post, "/cart/items", new { productId = 1, quantity = 1, requestId = Guid.NewGuid().ToString(), productName = "Falso", unitPrice = 0.01m });
        Assert.Equal(HttpStatusCode.OK, add.StatusCode);
        var cart = await add.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(49.90m, cart.GetProperty("items")[0].GetProperty("unitPrice").GetDecimal());

        using var checkout = await SendJson(client, HttpMethod.Post, "/cart/checkout", new { requestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.OK, checkout.StatusCode);
        var checkoutBody = await checkout.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Compra registrada com sucesso. Esta operação é uma simulação para fins acadêmicos. Nenhuma cobrança foi realizada.", checkoutBody.GetProperty("message").GetString());
        var history = await client.GetFromJsonAsync<JsonElement>("/user-actions/purchases");
        Assert.Equal(49.90m, history[0].GetProperty("unitPrice").GetDecimal());
        Assert.Equal("Compra registrada", history[0].GetProperty("situation").GetString());

        using var failedCheckout = await SendJson(client, HttpMethod.Post, "/cart/checkout", new { requestId = Guid.NewGuid().ToString() });
        Assert.Equal(HttpStatusCode.BadRequest, failedCheckout.StatusCode);
        Assert.DoesNotContain("Compra registrada com sucesso", await failedCheckout.Content.ReadAsStringAsync());

        using var logout = await SendJson(client, HttpMethod.Post, "/auth/logout", new { });
        Assert.Equal(HttpStatusCode.OK, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ReservationResponsesUseSimpleMessagesWithoutAcademicNotice()
    {
        using var client = _factory.CreateClient(new() { AllowAutoRedirect = false });
        await RegisterAndLogin(client, $"reservas-{Guid.NewGuid():N}@example.test");

        using var ticket = await SendJson(client, HttpMethod.Post, "/user-actions/tickets", new
        {
            sessionId = 1, quantity = 1, requestId = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.OK, ticket.StatusCode);
        var ticketBody = await ticket.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Ingresso reservado com sucesso.", ticketBody.GetProperty("message").GetString());

        using var appointment = await SendJson(client, HttpMethod.Post, "/user-actions/appointments", new
        {
            sessionId = 2, requestId = Guid.NewGuid().ToString()
        });
        Assert.Equal(HttpStatusCode.OK, appointment.StatusCode);
        var appointmentBody = await appointment.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Visita agendada com sucesso.", appointmentBody.GetProperty("message").GetString());

        var reservationId = appointmentBody.GetProperty("id").GetInt32();
        using var cancellation = await SendJson(client, HttpMethod.Delete, $"/user-actions/reservations/{reservationId}", new { });
        Assert.Equal(HttpStatusCode.OK, cancellation.StatusCode);
        Assert.Contains("Agendamento cancelado com sucesso.", await cancellation.Content.ReadAsStringAsync());
    }

    private static async Task RegisterAndLogin(HttpClient client, string email)
    {
        using var registration = await SendJson(client, HttpMethod.Post, "/auth/register", new { name = "Pessoa Teste", email, password = "senha-ficticia-123" });
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
