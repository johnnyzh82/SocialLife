using Facebook;
using Newtonsoft.Json;
using SocialLife.Extensions;
using SocialLife.Models;
using System.Collections.Generic;
using System.Configuration;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace SocialLife.Controllers
{
    [Authorize]
    public class FacebookSearchController : BaseController
    {
        // GET: FacebookSearch
        public ActionResult Index()
        {
            return View();
        }

        [HttpPost]
        public async Task<ActionResult> Find(string QueryValue,string SearchBy,string SearchCenterCoordinates)
        {
            #region Permission Check
            if (base.CheckPermission())
            {
                //Check all permission here to avoid missing a permission from 
                //one of the Ajax based Action Methods
                PermissionRequestViewModel permissionViewModel = new
                    PermissionRequestViewModel();
                permissionViewModel.MissingPermissions = base.CheckPermissions(GetRequiredPermissions());
                if (permissionViewModel.MissingPermissions.Any())
                {
                    return View("FB_RequestPermission", permissionViewModel);
                }
            }
            #endregion

            #region Facebook Graph API Search call
            var accessToken = HttpContext.Items["access_token"].ToString();
            if (!string.IsNullOrEmpty(accessToken))
            {
                var appsecret_proof = accessToken.GenerateAppSecretProof();
                var fb = new FacebookClient(accessToken);

                #region Search For Place
                string _searchCenterParam = string.Empty;
                if (SearchBy == "place" &&
                    !string.IsNullOrEmpty(SearchCenterCoordinates))
                    _searchCenterParam = string.Format("&center={0}&distance={1}",
                        SearchCenterCoordinates,
                        ConfigurationManager.AppSettings["Facebook_Distance_Meters"].ToString());
                #endregion

                #region Search Facebook Graph Query call
                dynamic myInfo = await fb.GetTaskAsync(
                    (string.Format("search?q={0}&type={1}{2}&limit=50",
                                    QueryValue,
                                    SearchBy,
                                    _searchCenterParam) +
                        "&fields=name,id,picture.type(large).width(100).height(100){{url}}")
                        .GraphAPICall(appsecret_proof));
                #endregion

                #region Hydrate results
                //Hydrate FacebookProfileViewModel with Graph API results
                var userList = new List<FacebookFriendViewModel>();
                foreach (dynamic friend in myInfo.data)
                {
                    userList.Add(DynamicExtension.ToStatic<FacebookFriendViewModel>(friend));
                }


                #endregion

                #region Paging Results
                string NextPageURI = string.Empty;

                if (myInfo.paging != null && myInfo.paging.next != null)
                    NextPageURI = myInfo.paging.next;

                ViewBag.ShowGetMoreData = GetNextPageQuery(NextPageURI, accessToken);
                #endregion

                return PartialView("FindResults", userList);

            }
            throw new HttpException(404, "Missing Access Token");
            #endregion
        }

        private string GetNextPageQuery(string NextPageURI, string access_token)
        {
            string ReturnNextPageURI = NextPageURI
                    .Replace("https://graph.facebook.com/v2.8/", "")
                    .Replace(string.Format("&access_token={0}", access_token), "");

            return ReturnNextPageURI;
        }

        [HttpPost]
        public async Task<ActionResult> GetMoreData(string NextPageUri)
        {
            if (!string.IsNullOrEmpty(NextPageUri))
            {
                #region Permission Check
                if (base.CheckPermission())
                {
                    //Check all permission here to avoid missing a permission from 
                    //one of the Ajax based Action Methods
                    PermissionRequestViewModel permissionViewModel = new
                        PermissionRequestViewModel();
                    permissionViewModel.MissingPermissions = base.CheckPermissions(GetRequiredPermissions());
                    if (permissionViewModel.MissingPermissions.Any())
                    {
                        return View("FB_RequestPermission", permissionViewModel);
                    }
                }
                #endregion

                #region Get More Paged Data
                var accessToken = HttpContext.Items["access_token"].ToString();
                if (!string.IsNullOrEmpty(accessToken))
                {
                    var appsecret_proof = accessToken.GenerateAppSecretProof();
                    var fb = new FacebookClient(accessToken);
                    dynamic nextPageResult = await fb.GetTaskAsync(NextPageUri.GraphAPICall(appsecret_proof));

                    #region Hydrate results
                    //Hydrate FacebookProfileViewModel with Graph API results
                    var userList = new List<FacebookFriendViewModel>();
                    foreach (dynamic friend in nextPageResult.data)
                    {

                        userList.Add(DynamicExtension.ToStatic<FacebookFriendViewModel>(friend));
                    }
                    #endregion

                    #region Paging Results
                    string NextPageURI = string.Empty;

                    if (nextPageResult.paging != null &&
                        nextPageResult.paging.next != null)
                        NextPageURI = nextPageResult.paging.next;

                    ViewBag.ShowGetMoreData = GetNextPageQuery(NextPageURI, accessToken);
                    #endregion
                    return PartialView("FindResults", userList);
                }
                throw new HttpException(404, "Missing Access Token");
                #endregion
            }
            return null;
        }

        [HttpPost]
        public ActionResult GeoCode(string LocationSearch)
        {
            WebRequest request = WebRequest.Create(string.Format(
                "http://dev.virtualearth.net/REST/v1/Locations?query={0}&output=json&key={1}",
                Server.UrlEncode(LocationSearch),
                ConfigurationManager.AppSettings["BingMapKey"].ToString()));
            request.Method = WebRequestMethods.Http.Get;

            WebResponse response = request.GetResponse();

            StreamReader sr = new StreamReader(response.GetResponseStream());
            string _recievBuffer = sr.ReadToEnd();
            dynamic geocodeResults = JsonConvert.DeserializeObject(_recievBuffer);

            #region Hydrate Results
            if (geocodeResults != null &&
                geocodeResults.resourceSets != null &&
                geocodeResults.resourceSets[0] != null &&
                geocodeResults.resourceSets[0].resources != null)
            {
                //Hydrate GeoCodeLocationViewModel with Geo Code results
                var locationList = new List<GeoCodeLocationViewModel>();
                foreach (dynamic resource in geocodeResults.resourceSets[0].resources)
                {
                    locationList.Add(new GeoCodeLocationViewModel()
                    {
                        Name = resource.name.Value,
                        Coordinates = string.Format("{0},{1}",resource.point.coordinates[0].ToString(),resource.point.coordinates[1].ToString())
                    });
                }
                #endregion
                return PartialView("GeoCodeResults", locationList);
            }
            return new HttpStatusCodeResult(HttpStatusCode.BadRequest, "Invalid GeoCode Request");
        }

        private Dictionary<string, FacebookPermissionRequest> GetRequiredPermissions()
        {
            Dictionary<string, FacebookPermissionRequest> RequiredPermissions = new Dictionary<string, FacebookPermissionRequest>
            {
                {
                    "public_profile", new FacebookPermissionRequest
                    {
                        name = "Basic Searching",
                        description =
                            "Provides the Social Manager with ability to search for people, pages, events, groups or places.",
                        permision_scope_value = "public_profile",
                        requested = false
                    }
                }
            };
            return RequiredPermissions;
        }
    }
}