using Facebook;
using System;
using System.Linq.Expressions;
using System.Text.RegularExpressions;
using System.Web.Mvc;
using System.Web.Mvc.Html;


namespace SocialLife.Extensions
{
    public static class FacebookHtmlExtensions
    {
        public static string GetPropertyPath<TEntity, TProperty>(Expression<Func<TEntity, TProperty>> property)
        {
            Match match = Regex.Match(property.ToString(), @"^[^\.]+\.([^\(\)]+)$");
            return match.Groups[1].Value;
        }

        public static MvcHtmlString FB_AgeRangeFor<TEntity>(this HtmlHelper html, TEntity model, Expression<Func<TEntity, JsonObject>> property, object htmlAttributes)
        {
            JsonObject age_range = property.Compile().Invoke(model);

            //here we can format our age range value
            string age_rangeFormatted = string.Empty;
            if (age_range != null)
            {
                if (age_range.Keys.Contains("min"))
                {
                    age_rangeFormatted = age_range["min"].ToString();
                }
                if (age_range.Keys.Contains("min") &&
                    age_range.Keys.Contains("max"))
                {
                    age_rangeFormatted = age_rangeFormatted + " to " + age_range["min"].ToString();
                }
                else if (age_range.Keys.Contains("max"))
                {
                    age_rangeFormatted = age_range["max"].ToString();
                }

            }
            var name = GetPropertyPath(property);

            return html.TextBox(name, age_rangeFormatted, htmlAttributes);
        }
    }
}