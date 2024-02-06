namespace AniWorldAutoDL_Webpanel.Interfaces
{
    internal interface IApiService
    {
        Task<bool> Login(string username, string password);
        Task<T?> GetAsync<T>(string uri);
        Task<T?> GetAsync<T>(string uri, Dictionary<string, string> queryData, object body);
        Task<T?> GetAsync<T>(string uri, Dictionary<string, string> queryData);
        Task<T?> PostAsync<T>(string uri, object value);
        Task<bool> PostAsync(string uri, object value);
    }
}
