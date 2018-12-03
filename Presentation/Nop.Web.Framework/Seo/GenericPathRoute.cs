using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.Routing.Template;
using Nop.Core;
using Nop.Core.Data;
using Nop.Core.Domain.Localization;
using Nop.Core.Infrastructure;
using Nop.Services.Events;
using Nop.Services.Seo;
using Nop.Web.Framework.Localization;

namespace Nop.Web.Framework.Seo
{
    /// <summary>
    /// Provides properties and methods for defining a SEO friendly route, and for getting information about the route.
    /// </summary>
    public class GenericPathRoute : LocalizedRoute
    {
        #region Fields

        private readonly IRouter _target;

        private readonly List<string> _redirectHomeSlugs = new List<string>
        {
            "best-sales",
            "forklifts",
            "niuli-25-tonne-diesel-forklift",
            "manufacturers",
            "prices-drop",
            "supplier",
            "safes"
        };

        #endregion

        #region Ctor

        /// <summary>
        /// Ctor
        /// </summary>
        /// <param name="target">Target</param>
        /// <param name="routeName">Route name</param>
        /// <param name="routeTemplate">Route remplate</param>
        /// <param name="defaults">Defaults</param>
        /// <param name="constraints">Constraints</param>
        /// <param name="dataTokens">Data tokens</param>
        /// <param name="inlineConstraintResolver">Inline constraint resolver</param>
        public GenericPathRoute(IRouter target, string routeName, string routeTemplate, RouteValueDictionary defaults, 
            IDictionary<string, object> constraints, RouteValueDictionary dataTokens, IInlineConstraintResolver inlineConstraintResolver)
            : base(target, routeName, routeTemplate, defaults, constraints, dataTokens, inlineConstraintResolver)
        {
            _target = target ?? throw new ArgumentNullException(nameof(target));
        }

        #endregion

        #region Utilities

        /// <summary>
        /// Get route values for current route
        /// </summary>
        /// <param name="context">Route context</param>
        /// <returns>Route values</returns>
        protected RouteValueDictionary GetRouteValues(RouteContext context)
        {
            //remove language code from the path if it's localized URL
            var path = context.HttpContext.Request.Path.Value;
            if (this.SeoFriendlyUrlsForLanguagesEnabled && path.IsLocalizedUrl(context.HttpContext.Request.PathBase, false, out Language _))
                path = path.RemoveLanguageSeoCodeFromUrl(context.HttpContext.Request.PathBase, false);

            //parse route data
            var routeValues = new RouteValueDictionary(this.ParsedTemplate.Parameters
                .Where(parameter => parameter.DefaultValue != null)
                .ToDictionary(parameter => parameter.Name, parameter => parameter.DefaultValue));
            var matcher = new TemplateMatcher(this.ParsedTemplate, routeValues);
            matcher.TryMatch(path, routeValues);

            return routeValues;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Route request to the particular action
        /// </summary>
        /// <param name="context">A route context object</param>
        /// <returns>Task of the routing</returns>
        public override Task RouteAsync(RouteContext context)
        {
            if (!DataSettingsHelper.DatabaseIsInstalled())
                return Task.CompletedTask;

            //try to get slug from the route data
            var routeValues = GetRouteValues(context);
            if (!routeValues.TryGetValue("GenericSeName", out object slugValue) || string.IsNullOrEmpty(slugValue as string))
            {
                var slugTokens = context.HttpContext.Request.Path.Value.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (slugTokens.Length < 2)
                    return Task.CompletedTask;

                var routeData = GetProductWithCategoryRouteData(context, slugTokens);
                if (routeData == null)
                    return Task.CompletedTask;

                context.RouteData = routeData;
                return _target.RouteAsync(context);
            }

            var slug = (string) slugValue;

            //virtual directory path
            var pathBase = context.HttpContext.Request.PathBase;

            if (slug == "pallet-jacks-stackers" || slug == "forklift-ramps")
            {
                var action = slug == "pallet-jacks-stackers" ? "pallet-stackers" : "container-ramps";
                var redirectionRouteData = new RouteData(context.RouteData);
                redirectionRouteData.Values["controller"] = "Common";
                redirectionRouteData.Values["action"] = "InternalRedirect";
                redirectionRouteData.Values["url"] = $"{pathBase}/{action}";
                redirectionRouteData.Values["permanentRedirect"] = true;
                context.HttpContext.Items["nop.RedirectFromGenericPathRoute"] = true;
                context.RouteData = redirectionRouteData;
                return _target.RouteAsync(context);
            }

            if (_redirectHomeSlugs.Contains(slug))
            {
                context.RouteData = RedirectHome(context);
                return _target.RouteAsync(context);
            }

            //performance optimization, we load a cached verion here. It reduces number of SQL requests for each page load
            var urlRecordService = EngineContext.Current.Resolve<IUrlRecordService>();
            var urlRecord = urlRecordService.GetBySlugCached(slug);
            //comment the line above and uncomment the line below in order to disable this performance "workaround"
            //var urlRecord = urlRecordService.GetBySlug(slug);

            //no URL record found
            if (urlRecord == null)
                return Task.CompletedTask;

            //if URL record is not active let's find the latest one
            if (!urlRecord.IsActive)
            {
                var activeSlug = urlRecordService.GetActiveSlug(urlRecord.EntityId, urlRecord.EntityName, urlRecord.LanguageId);
                if (string.IsNullOrEmpty(activeSlug))
                    return Task.CompletedTask;

                //redirect to active slug if found
                var redirectionRouteData = new RouteData(context.RouteData);
                redirectionRouteData.Values["controller"] = "Common";
                redirectionRouteData.Values["action"] = "InternalRedirect";
                redirectionRouteData.Values["url"] = $"{pathBase}/{activeSlug}{context.HttpContext.Request.QueryString}";
                redirectionRouteData.Values["permanentRedirect"] = true;
                context.HttpContext.Items["nop.RedirectFromGenericPathRoute"] = true;
                context.RouteData = redirectionRouteData;
                return _target.RouteAsync(context);
            }

            //ensure that the slug is the same for the current language, 
            //otherwise it can cause some issues when customers choose a new language but a slug stays the same
            var workContext = EngineContext.Current.Resolve<IWorkContext>();
            var slugForCurrentLanguage = SeoExtensions.GetSeName(urlRecord.EntityId, urlRecord.EntityName, workContext.WorkingLanguage.Id);
            if (!string.IsNullOrEmpty(slugForCurrentLanguage) && !slugForCurrentLanguage.Equals(slug, StringComparison.InvariantCultureIgnoreCase))
            {
                //we should make validation above because some entities does not have SeName for standard (Id = 0) language (e.g. news, blog posts)

                //redirect to the page for current language
                var redirectionRouteData = new RouteData(context.RouteData);
                redirectionRouteData.Values["controller"] = "Common";
                redirectionRouteData.Values["action"] = "InternalRedirect";
                redirectionRouteData.Values["url"] = $"{pathBase}/{slugForCurrentLanguage}{context.HttpContext.Request.QueryString}";
                redirectionRouteData.Values["permanentRedirect"] = false;
                context.HttpContext.Items["nop.RedirectFromGenericPathRoute"] = true;
                context.RouteData = redirectionRouteData;
                return _target.RouteAsync(context);
            }

            //since we are here, all is ok with the slug, so process URL
            var currentRouteData = new RouteData(context.RouteData);
            switch (urlRecord.EntityName.ToLowerInvariant())
            {
                case "product":
                    currentRouteData.Values["controller"] = "Product";
                    currentRouteData.Values["action"] = "ProductDetails";
                    currentRouteData.Values["productid"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "category":
                    currentRouteData.Values["controller"] = "Catalog";
                    currentRouteData.Values["action"] = "Category";
                    currentRouteData.Values["categoryid"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "manufacturer":
                    currentRouteData.Values["controller"] = "Catalog";
                    currentRouteData.Values["action"] = "Manufacturer";
                    currentRouteData.Values["manufacturerid"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "vendor":
                    currentRouteData.Values["controller"] = "Catalog";
                    currentRouteData.Values["action"] = "Vendor";
                    currentRouteData.Values["vendorid"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "newsitem":
                    currentRouteData.Values["controller"] = "News";
                    currentRouteData.Values["action"] = "NewsItem";
                    currentRouteData.Values["newsItemId"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "blogpost":
                    currentRouteData.Values["controller"] = "Blog";
                    currentRouteData.Values["action"] = "BlogPost";
                    currentRouteData.Values["blogPostId"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                case "topic":
                    currentRouteData.Values["controller"] = "Topic";
                    currentRouteData.Values["action"] = "TopicDetails";
                    currentRouteData.Values["topicId"] = urlRecord.EntityId;
                    currentRouteData.Values["SeName"] = urlRecord.Slug;
                    break;
                default:
                    //no record found, thus generate an event this way developers could insert their own types
                    EngineContext.Current.Resolve<IEventPublisher>().Publish(new CustomUrlRecordEntityNameRequested(currentRouteData, urlRecord));
                    break;
            }
            context.RouteData = currentRouteData;

            //route request
            return _target.RouteAsync(context);
        }

        private static RouteData RedirectHome(RouteContext context)
        {
            var redirectionRouteData = new RouteData(context.RouteData);
            redirectionRouteData.Values["controller"] = "Common";
            redirectionRouteData.Values["action"] = "InternalRedirect";
            redirectionRouteData.Values["url"] = context.HttpContext.Request.PathBase;
            redirectionRouteData.Values["permanentRedirect"] = true;
            context.HttpContext.Items["nop.RedirectFromGenericPathRoute"] = true;
            return redirectionRouteData;
        }

        private RouteData GetProductWithCategoryRouteData(RouteContext context, IReadOnlyCollection<string> slugTokens)
        {
            var urlRecordService = EngineContext.Current.Resolve<IUrlRecordService>();
            var productToken = slugTokens.Last();

            if (_redirectHomeSlugs.Contains(productToken))
                return RedirectHome(context);

            if (productToken == "extended-container-ramp")
            {
                var redirectionRouteData = new RouteData(context.RouteData);
                redirectionRouteData.Values["controller"] = "Common";
                redirectionRouteData.Values["action"] = "InternalRedirect";
                redirectionRouteData.Values["url"] = $"{context.HttpContext.Request.PathBase}/container-ramp-extended";
                redirectionRouteData.Values["permanentRedirect"] = true;
                context.HttpContext.Items["nop.RedirectFromGenericPathRoute"] = true;

                return redirectionRouteData;
            }

            var productRecord = urlRecordService.GetBySlug(productToken);
            if (productRecord == null || !productRecord.IsActive || productRecord.EntityName.ToLowerInvariant() != "product")
                return null;

            var currentRouteData = new RouteData(context.RouteData);
            currentRouteData.Values["controller"] = "Product";
            currentRouteData.Values["action"] = "ProductDetails";
            currentRouteData.Values["productid"] = productRecord.EntityId;
            currentRouteData.Values["SeName"] = productRecord.Slug;

            return currentRouteData;
        }

//        REPLACE WITH ABOVE METHOD IF NESTING FOLDERS
//        private static RouteData GetProductWithCategoryRouteData(RouteContext context, IReadOnlyCollection<string> slugTokens)
//        {
//            var urlRecordService = EngineContext.Current.Resolve<IUrlRecordService>();
//            var categoryTokens = slugTokens.Take(slugTokens.Count - 1);
//            var productToken = slugTokens.ElementAt(slugTokens.Count - 1);
//            var allMatch = true;
//            var categoryIds = new List<int>();
//            foreach (var categoryToken in categoryTokens)
//            {
//                var categoryRecord = urlRecordService.GetBySlug(categoryToken);
//                if (categoryRecord == null ||
//                    !categoryRecord.IsActive ||
//                    categoryRecord.EntityName.ToLowerInvariant() != "category")
//                    allMatch = false;
//
//                if (!allMatch) break;
//
//                categoryIds.Add(categoryRecord.EntityId);
//            }
//            var productRecord = urlRecordService.GetBySlug(productToken);
//            if (productRecord == null ||
//                !productRecord.IsActive ||
//                productRecord.EntityName.ToLowerInvariant() != "product")
//                allMatch = false;
//
//            if (allMatch)
//            {
//                var categoryService = EngineContext.Current.Resolve<ICategoryService>();
//                var productService = EngineContext.Current.Resolve<IProductService>();
//                var product = productService.GetProductById(productRecord.EntityId);
//                if (product != null)
//                {
//                    var firstCategory = product.ProductCategories
//                        .Where(p => !p.Category.Deleted)
//                        .OrderBy(x => x.DisplayOrder).FirstOrDefault();
//                    var hierarchyMatch = true;
//                    if (firstCategory != null &&
//                        firstCategory.CategoryId == categoryIds[categoryIds.Count - 1])
//                    {
//                        for (var j = categoryIds.Count - 1; j >= 0; j--)
//                        {
//                            var current = categoryService.GetCategoryById(categoryIds[j]);
//                            if (current != null)
//                            {
//                                if (j > 0 && current.ParentCategoryId != categoryIds[j - 1])
//                                    hierarchyMatch = false;
//                            }
//                            else
//                            {
//                                hierarchyMatch = false;
//                            }
//
//                            if (!hierarchyMatch) break;
//                        }
//                    }
//                    else
//                    {
//                        hierarchyMatch = false;
//                    }
//
//                    if (hierarchyMatch)
//                    {
//                        var currentRouteData = new RouteData(context.RouteData);
//                        currentRouteData.Values["controller"] = "Product";
//                        currentRouteData.Values["action"] = "ProductDetails";
//                        currentRouteData.Values["productid"] = productRecord.EntityId;
//                        currentRouteData.Values["SeName"] = productRecord.Slug;
//
//                        return currentRouteData;
//                    }
//                }
//            }
//
//            return null;
//        }

        #endregion
    }
}