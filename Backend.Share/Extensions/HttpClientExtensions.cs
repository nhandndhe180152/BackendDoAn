using System;
using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;

namespace Backend.Share.Extensions;

public static class HttpClientExtensions
{
    public async static Task<TResponse?> SendRequestAsync<TRequest, TResponse>(this HttpClient httpClient, string url, TRequest request)
        where TRequest : class
    {

        var json = JsonConvert.SerializeObject(request);
        var content = new StringContent(json, Encoding.UTF8, "application/json");
        var response = await httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException($"Something went wrong calling the API: {response.ReasonPhrase}");
        }
        var result = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(result, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,

        });
    }

    public async static Task<TResponse?> SendRequestAsync<TResponse>(this HttpClient httpClient, string url)
    {
        var response = await httpClient.GetAsync(url);
        if (!response.IsSuccessStatusCode)
        {
            throw new ApplicationException($"Something went wrong calling the API: {response.ReasonPhrase}");
        }
        var result = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(result, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
    }

    public async static Task<TResponse?> PostFileAsync<TResponse>(this HttpClient httpClient, string url,
        IEnumerable<IFormFile> files)
    {
        using var content = new MultipartFormDataContent();

        foreach (var file in files)
        {
            var stream = file.OpenReadStream();
            var fileContent = new StreamContent(stream);
            fileContent.Headers.ContentType = new MediaTypeHeaderValue(file.ContentType);
            content.Add(fileContent, "files", file.FileName);
        }

        var response = await httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApplicationException($"Something went wrong calling the API: {response.ReasonPhrase} with message: {message}");
        }
        var result = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(result, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
    }

    public async static Task<TResponse?> PostFileAsync<TResponse>(this HttpClient httpClient, string url,
        IFormFile file)
    {
        using var content = new MultipartFormDataContent();
        await using var stream = file.OpenReadStream();
        content.Add(new StreamContent(stream), "file", file.FileName);

        var response = await httpClient.PostAsync(url, content);
        if (!response.IsSuccessStatusCode)
        {
            var message = await response.Content.ReadAsStringAsync();
            throw new ApplicationException($"Something went wrong calling the API: {response.ReasonPhrase} with message: {message}");
        }
        var result = await response.Content.ReadAsStringAsync();
        return JsonConvert.DeserializeObject<TResponse>(result, new JsonSerializerSettings
        {
            ReferenceLoopHandling = ReferenceLoopHandling.Ignore,
        });
    }
}