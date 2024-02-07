using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.Net.Http.Headers;
using System.Net;
using System.Text;
using AniWorldAutoDL_Webpanel.Interfaces;

namespace AniWorldAutoDL_Webpanel.Services
{
    public class ApiService(ILogger<ApiService> logger, HttpClient httpClient)
        : IApiService
    {
        private bool IsInitialized;

        public bool Init()
        {
            SettingsModel? settings = SettingsHelper.ReadSettings<SettingsModel>();

            if (settings is null || string.IsNullOrEmpty(settings.ApiUrl))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettings}");
                return false;
            }

            if (!Uri.TryCreate(settings.ApiUrl, UriKind.Absolute, out Uri? apiUri))
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.ReadSettingsApiUrl}");
                return false;
            }

            httpClient.BaseAddress = apiUri;

            IsInitialized = true;

            logger.LogInformation($"{DateTime.Now} | {InfoMessage.ApiServiceInit}");

            return true;
        }

        public async Task<bool> Login(string username, string password)
        {
            JwtResponseModel? jwtResponse = await PostAsync<JwtResponseModel>("login", new UserModel() { Username = username, Password = password });

            if (jwtResponse is null || string.IsNullOrEmpty(jwtResponse.Token))
            {
                UserStorageHelper.Set(default!);
                return false;
            }

            UserModel user = new()
            {
                Token = jwtResponse.Token,
                Username = username
            };

            UserStorageHelper.Set(user);

            return true;
        }
        public async Task<T?> GetAsync<T>(string uri)
        {
            HttpRequestMessage request = new(HttpMethod.Get, uri);
            return await SendRequest<T>(request);
        }

        public async Task<T?> GetAsync<T>(string uri, Dictionary<string, string> queryData, object body)
        {
            HttpRequestMessage request = new(HttpMethod.Get, new Uri(QueryHelpers.AddQueryString(httpClient.BaseAddress + uri, queryData!)))
            {
                Content = new StringContent(JsonConvert.SerializeObject(body), Encoding.UTF8, "application/json")
            };
            return await SendRequest<T>(request);
        }
        public async Task<T?> GetAsync<T>(string uri, Dictionary<string, string> queryData)
        {
            HttpRequestMessage request = new(HttpMethod.Get, new Uri(QueryHelpers.AddQueryString(httpClient.BaseAddress + uri, queryData!)));
            return await SendRequest<T>(request);
        }

        public async Task<T?> PostAsync<T>(string uri, object value)
        {
            HttpRequestMessage request = new(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json")
            };
            return await SendRequest<T>(request);
        }

        public async Task<bool> PostAsync(string uri, object value)
        {
            HttpRequestMessage request = new(HttpMethod.Post, uri)
            {
                Content = new StringContent(JsonConvert.SerializeObject(value), Encoding.UTF8, "application/json")
            };
            return await SendRequest<bool>(request);
        }

        private async Task<T?> SendRequest<T>(HttpRequestMessage request)
        {
            if (!IsInitialized)
            {
                logger.LogError($"{DateTime.Now} | {ErrorMessage.APIServiceNotInitialized}");
                return default;
            }

            UserModel? user = UserStorageHelper.Get();

            if (user is not null && !string.IsNullOrEmpty(user.Token))
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);

            using HttpResponseMessage? response = await httpClient.SendAsync(request);

            // auto logout on 401 response
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                UserStorageHelper.Set(default!);
                return default;
            }

            // throw exception on error response
            if (!response.IsSuccessStatusCode)
                return default;

            if (typeof(T) == typeof(bool))
            {
                return (T)Convert.ChangeType(response.IsSuccessStatusCode, typeof(T));
            }

            return JsonConvert.DeserializeObject<T>(await response.Content.ReadAsStringAsync());
        }
    }
}
