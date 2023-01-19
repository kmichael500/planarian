using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using Planarian.Shared.Exceptions;
using Planarian.Shared.Options;

namespace Planarian.Shared.Email.Services;

public class MjmlService
{
    private readonly HttpClient _httpClient;

    public MjmlService(HttpClient httpClient, EmailOptions options)
    {
        _httpClient = httpClient;
        var byteArray = Encoding.ASCII.GetBytes($"{options.MjmlApplicationId}:{options.MjmlSecretKey}");
        _httpClient.DefaultRequestHeaders.Authorization =
            new AuthenticationHeaderValue("Basic", Convert.ToBase64String(byteArray));
        _httpClient.BaseAddress = new Uri("https://api.mjml.io/v1/");
    }

    public async Task<string> MjmlToHtml(string mjml)
    {

        var request = new HttpRequestMessage(HttpMethod.Post, "render")
        {
            Content = new StringContent(JsonConvert.SerializeObject(new { Mjml = mjml }), Encoding.UTF8,
                "application/json")
        };

        var response = await _httpClient.SendAsync(request);

        if (!response.IsSuccessStatusCode)
        {
            throw ApiExceptionDictionary.BadRequest(response.ReasonPhrase ?? "Mjml error");
        }

        var result = await response.Content.ReadFromJsonAsync<MjmlResponse>();

        if (result == null)
        {
            throw ApiExceptionDictionary.BadRequest("Mjml error");
        }

        return result.Html;
    }

    private class MjmlResponse
    {
        public string[] Errors { get; set; }
        public string Html { get; set; }
        public string Mjml { get; set; }
        public string Mjml_version { get; set; }
    }
}