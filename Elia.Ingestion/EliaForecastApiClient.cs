using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Elia.Ingestion;

public class EliaForecastApiClient
{
    private readonly HttpClient _httpClient;

    public EliaForecastApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    public async Task<string> GetPvForecastRawAsync(int limit = 100)
    {
        // elia API requires -1 <= limit <= 100
        if (limit > 100)
        {
            limit = 100;
        }
        if (limit < -1)
        {
            limit = -1;
        }

        var url = $"/api/explore/v2.1/catalog/datasets/ods087/records?limit={limit}";
        Console.WriteLine($"Requesting (PV): {url}");

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"Elia PV API error {(int)response.StatusCode} {response.ReasonPhrase}. Response body: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Received {json.Length} characters of JSON (PV).");
        return json;
    }

    public async Task<string> GetWindForecastRawAsync(int limit = 100)
    {
        if (limit > 100)
        {
            limit = 100;
        }
        if (limit < -1)
        {
            limit = -1;
        }

        var url = $"/api/explore/v2.1/catalog/datasets/ods086/records?limit={limit}";
        Console.WriteLine($"Requesting (Wind): {url}");

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception($"Elia Wind API error {(int)response.StatusCode} {response.ReasonPhrase}. Response body: {body}");

        }
        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Received {json.Length} characters of JSON (Wind).");
        return json;
    }
}

