using System;
using System.Web;
using EPiServer;
using EPiServer.Core;
using EPiServer.ServiceLocation;
using EPiServer.Web;
using EPiServer.Web.Routing;
using Geta.ImageOptimization.Configuration;

namespace Geta.ImageOptimization.Helpers
{
    public static class ContentExtensions
    {
        public static string GetFriendlyUrl(this ContentReference contentReference)
        {
            if (ContentReference.IsNullOrEmpty(contentReference))
            {
                return string.Empty;
            }

            var urlResolver = ServiceLocator.Current.GetInstance<UrlResolver>();

            var url = urlResolver.GetUrl(contentReference);

            return GetExternalUrl(url);
        }

        private static string GetExternalUrl(string input)
        {
            var siteUri = HttpContext.Current != null
                            ? HttpContext.Current.Request.Url
                            : SiteDefinition.Current.SiteUrl;

            if (!string.IsNullOrEmpty(ImageOptimizationSettings.Instance.SiteUrl))
            {
                siteUri = new Uri(ImageOptimizationSettings.Instance.SiteUrl);
            }

            var urlBuilder = new UrlBuilder(input)
            {
                Scheme = siteUri.Scheme,
                Host = siteUri.Host,
                Port = siteUri.Port
            };

            return urlBuilder.ToString();
        }
    }
}