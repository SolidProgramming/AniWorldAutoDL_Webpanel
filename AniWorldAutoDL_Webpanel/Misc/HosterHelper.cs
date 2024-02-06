using System.Text.RegularExpressions;
using HtmlAgilityPack;

namespace AniWorldAutoDL_Webpanel.Misc
{
    internal static class HosterHelper
    {
        private static readonly List<HosterModel> SupportedHoster = new()
        {
            { new HosterModel("s.to", Hoster.STO, "https://s.to/") },
            { new HosterModel("aniworld.to", Hoster.AniWorld, "https://aniworld.to/") },
        };

        internal static async Task<bool> HosterReachable(HosterModel hoster)
        {
            try
            {
                using HttpClient client = new();
                client.Timeout = TimeSpan.FromSeconds(5);

                HttpResponseMessage responseMessage = await client.GetAsync(hoster.BrowserUrl);

                if (!responseMessage.IsSuccessStatusCode)
                    return false;

                string html = await responseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrEmpty(html))
                    return false;

                return !CaptchaRequired(html);
            }
            catch (Exception)
            {
                return false;
            }
        }

        internal static async Task<string?> GetEpisodeM3U8(string streamUrl)
        {
            try
            {
                using HttpClient httpClient = new();
                string html = await httpClient.GetStringAsync(streamUrl);

                HtmlDocument htmlDocument = new();
                htmlDocument.LoadHtml(html);

                return new Regex("'hls': '(.*?)',").Match(html).Groups[1].Value;
            }
            catch (Exception)
            {
                return null;
            }
        }

        //ToDo: Hoster Name dynamisch in den query einbinden
        internal static Dictionary<Language, List<string>> GetLanguageRedirectLinks(string html)
        {
            Dictionary<Language, List<string>> languageRedirectLinks = new();

            HtmlDocument document = new();
            document.LoadHtml(html);

            List<HtmlNode> languageRedirectNodes = new HtmlNodeQueryBuilder()
                .Query(document)
                    .GetNodesByQuery("//div/a/i[@title='Hoster VOE']");

            if (languageRedirectNodes == null || languageRedirectNodes.Count == 0)
                return null;

            List<string> redirectLinks;


            redirectLinks = GetLanguageRedirectLinksNodes(Language.GerDub);

            if (redirectLinks.Count > 0)
            {
                languageRedirectLinks.Add(Language.GerDub, redirectLinks);
            }

            redirectLinks = GetLanguageRedirectLinksNodes(Language.EngDub);

            if (redirectLinks.Count > 0)
            {
                languageRedirectLinks.Add(Language.EngDub, redirectLinks);
            }

            redirectLinks = GetLanguageRedirectLinksNodes(Language.EngSub);

            if (redirectLinks.Count > 0)
            {
                languageRedirectLinks.Add(Language.EngSub, redirectLinks);
            }

            redirectLinks = GetLanguageRedirectLinksNodes(Language.GerSub);

            if (redirectLinks.Count > 0)
            {
                languageRedirectLinks.Add(Language.GerSub, redirectLinks);
            }

            return languageRedirectLinks;


            List<string> GetLanguageRedirectLinksNodes(Language language)
            {
                List<HtmlNode> redirectNodes = languageRedirectNodes.Where(_ => _.ParentNode.ParentNode.ParentNode.Attributes["data-lang-key"].Value == language.ToVOELanguageKey())
                .ToList();
                List<string> filteredRedirectLinks = new();

                foreach (HtmlNode node in redirectNodes)
                {
                    if (node == null ||
                   node.ParentNode == null ||
                   node.ParentNode.ParentNode == null ||
                   node.ParentNode.ParentNode.ParentNode == null ||
                   !node.ParentNode.ParentNode.ParentNode.Attributes.Contains("data-link-target"))
                        continue;

                    filteredRedirectLinks.Add(node.ParentNode.ParentNode.ParentNode.Attributes["data-link-target"].Value);
                }

                return filteredRedirectLinks;
            }
        }

        private static bool CaptchaRequired(string html)
        {
            return html.Contains("Browser Check");
        }

        internal static HosterModel? GetHosterByEnum(Hoster hoster)
        {
            if (SupportedHoster.Any(h => h.Hoster == hoster))
            {
                return SupportedHoster.First(h => h.Hoster == hoster);
            }

            return default;
        }
    }
}
