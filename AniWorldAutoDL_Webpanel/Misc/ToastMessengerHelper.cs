using Microsoft.AspNetCore.Components.Rendering;
using Microsoft.AspNetCore.Components;
using System.Net;
using Havit.Blazor.Components.Web;
using Havit.Blazor.Components.Web.Bootstrap;

namespace AniWorldAutoDL_Webpanel.Misc
{
    public static class ToastMessengerHelper
    {
        public static void AddMessage(this IHxMessengerService messenger, string message, int autoHideDelay, ThemeColor themeColor = ThemeColor.Primary)
        {
            BootstrapMessengerMessage toast = new()
            {
                Color = themeColor,
                AutohideDelay = autoHideDelay,
                ContentTemplate = BuildContentTemplate("", message),
                CssClass = "mb-2"
            };

            messenger.AddMessage(toast);
        }

        private static RenderFragment BuildContentTemplate(string? title, string text)
        {
            return (RenderTreeBuilder builder) =>
            {
                if (title != null)
                {
                    builder.OpenElement(1, "div");
                    builder.AddAttribute(2, "class", "fw-bold");
                    builder.AddContent(3, ProcessLineEndings(title));
                    builder.CloseElement();
                }

                builder.AddContent(10, ProcessLineEndings(text));
            };
        }

        private static MarkupString ProcessLineEndings(string text)
        {
            return new MarkupString(WebUtility.HtmlEncode(text).ReplaceLineEndings("<br />"));
        }
    }
}
