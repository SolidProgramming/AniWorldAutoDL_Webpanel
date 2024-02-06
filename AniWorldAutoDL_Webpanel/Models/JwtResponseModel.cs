namespace AniWorldAutoDL_Webpanel.Models
{
    public class JwtResponseModel(string token)
    {
        public string Token { get; init; } = token;
    }
}
