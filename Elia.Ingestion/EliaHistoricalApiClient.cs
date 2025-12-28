using System;
using System.Net.Http;
using System.Threading.Tasks;

namespace Elia.Ingestion;

public class EliaHistoricalApiClient
{
    private readonly HttpClient _httpClient;

    public EliaHistoricalApiClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }

    // PV historical (ods032
    public async Task<string> GetPvHistoricalRawAsync(int limit = 100)
    {
        if (limit > 100) limit = 100;
        if (limit < -1) limit = -1;

        var url = $"/api/explore/v2.1/catalog/datasets/ods032/records?limit={limit}";
        Console.WriteLine($"Requesting (PV Historical): {url}");

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"External Elia PV historical API error {(int)response.StatusCode} {response.ReasonPhrase}. Response body: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Received {json.Length} characters of JSON (PV Historical).");
        return json;
    }

    // Wind historical (ods031)
    public async Task<string> GetWindHistoricalRawAsync(int limit = 100)
    {
        if (limit > 100) limit = 100;
        if (limit < -1) limit = -1;

        var url = $"/api/explore/v2.1/catalog/datasets/ods031/records?limit={limit}";
        Console.WriteLine($"Requesting (Wind Historical): {url}");

        var response = await _httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync();
            throw new Exception(
                $"External Elia wind historical API error {(int)response.StatusCode} {response.ReasonPhrase}. Response body: {body}");
        }

        var json = await response.Content.ReadAsStringAsync();
        Console.WriteLine($"Received {json.Length} characters of JSON (Wind Historical).");
        return json;
    }
}

