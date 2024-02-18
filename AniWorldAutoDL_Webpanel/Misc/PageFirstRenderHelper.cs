namespace AniWorldAutoDL_Webpanel.Misc
{
    public static class PageFirstRenderHelper
    {
        private static readonly List<string> List = [];
        private static readonly List<string> FirstRenderPages = List;

        public static bool IsFirstRender(this string componentName)
        {
            if (FirstRenderPages.Contains(componentName))
            {
                return false;
            }

            return true;
        }

        public static void ComponentSetRendered(string pageName)
        {
            if (!FirstRenderPages.Contains(pageName))
            {
                FirstRenderPages.Add(pageName);
            }
        }
    }
}
