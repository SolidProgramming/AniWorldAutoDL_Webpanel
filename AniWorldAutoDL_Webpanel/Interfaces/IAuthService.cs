namespace AniWorldAutoDL_Webpanel.Interfaces
{
    internal interface IAuthService
    {
        Task<bool> Login(string username, string password);
    }
}
