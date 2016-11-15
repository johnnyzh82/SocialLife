using Facebook;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.Owin;
using Newtonsoft.Json;
using SocialLife.Extensions;
using SocialLife.Filters;
using SocialLife.Models;
using System;
using System.Collections.Generic;
using System.Configuration;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace SocialLife.Controllers
{
    [Authorize]
    [FacebookAccessToken]
    public class BaseController : Controller
    {
        protected override void OnException(ExceptionContext filterContext)
        {
            if (filterContext.Exception is FacebookApiLimitException)
            {
                filterContext.ExceptionHandled = true;
                filterContext.Result = RedirectToAction("Index", "Message",
                    new MessageViewModel
                    {
                        Type = "Warning",
                        Message = "Facebook Graph API limit reached. Please try again later..."
                    });
            }
            else if (filterContext.Exception is FacebookOAuthException)
                if (HandleAsExpiredToken((FacebookOAuthException)filterContext.Exception))
                {
                    filterContext.ExceptionHandled = true;
                    filterContext.Result = GetFacebookLoginURL();
                }
                else
                {
                    //redirect to Facebook Custom Error Page
                    filterContext.ExceptionHandled = true;
                    filterContext.Result = RedirectToAction("Index", "Message",
                        new MessageViewModel
                        {
                            Type = "Error",
                            Message =
                            string.Format("{0} controller: {1}",
                                    filterContext.Exception.Source,
                                    filterContext.Exception.Message)
                        });
                }
            else if (filterContext.Exception is FacebookApiException)
            {
                //redirect to Facebook Custom Error Page
                filterContext.ExceptionHandled = true;
                filterContext.Result = RedirectToAction("Index", "Message",
                    new MessageViewModel
                    {
                        Type = "Error",
                        Message =
                            string.Format("{0} controller: {1}",
                                    filterContext.Exception.Source,
                                    filterContext.Exception.Message)
                    });
            }
            else
                base.OnException(filterContext);
        }



        private bool HandleAsExpiredToken(FacebookOAuthException OAuth_ex)
        {
            bool _HandleAsExpiredToken = false;
            if (OAuth_ex.ErrorCode == 190) //OAuthException
            {
                switch (OAuth_ex.ErrorSubcode)
                {
                    case 458: //App Not Installed
                    case 459: //User Checkpointed
                    case 460: //Password Changed
                    case 463: //Expired
                    case 464: //Unconfirmed User
                    case 467: //Invalid access token
                        _HandleAsExpiredToken = true;
                        break;
                    default:
                        _HandleAsExpiredToken = false;
                        break;
                }
            }
            else if (OAuth_ex.ErrorCode == 102)
            {
                //API Session. Login status or access token has expired, 
                //been revoked, or is otherwise invalid
                _HandleAsExpiredToken = true;
            }
            else if (OAuth_ex.ErrorCode == 10)
            {
                //API Permission Denied. Permission is either not granted or has been removed - 
                //Handle the missing permissions
                _HandleAsExpiredToken = false;
            }
            else if (OAuth_ex.ErrorCode >= 200 && OAuth_ex.ErrorCode <= 299)
            {
                //API Permission (Multiple values depending on permission). 
                //Permission is either not granted or has been removed - Handle the missing permissions
                _HandleAsExpiredToken = false;
            }
            return _HandleAsExpiredToken;
        }


        private RedirectResult GetFacebookLoginURL()
        {
            if (Session["AccessTokenRetryCount"] == null ||
                (Session["AccessTokenRetryCount"] != null &&
                 Session["AccessTokenRetryCount"].ToString() == "0"))
            {
                Session.Add("AccessTokenRetryCount", "1");

                FacebookClient fb = new FacebookClient();
                fb.AppId = ConfigurationManager.AppSettings["Facebook_AppId"];
                return Redirect(fb.GetLoginUrl(new
                {
                    scope = ConfigurationManager.AppSettings["Facebook_Scope"],
                    redirect_uri = RedirectUri.AbsoluteUri,
                    response_type = "code"
                }).ToString());
            }
            else
            {
                return Redirect(Url.Action("Index", "Message",
                    new MessageViewModel
                    {
                        Type = "Error",
                        Message = "Unable to obtain a valid Facebook Token after multiple attempts please contact support"
                    }));
            }
        }

        protected Uri RedirectUri
        {
            get
            {
                var uriBuilder = new UriBuilder(Request.Url);
                uriBuilder.Query = null;
                uriBuilder.Fragment = null;
                uriBuilder.Path = Url.Action("ExternalCallBack", "Base");
                return uriBuilder.Uri;
            }
        }


        public async Task<ActionResult> ExternalCallBack(string code)
        {
            //Callback return from Facebook will include a unique login encrypted code
            //for this user's login with our application id
            //that we can use to obtain a new access token
            FacebookClient fb = new FacebookClient();
            //Exchange encrypted login code for an access_token
            dynamic newTokenResult = await fb.GetTaskAsync(
                                        string.Format("oauth/access_token?client_id={0}&client_secret={1}&redirect_uri={2}&code={3}",
                                        ConfigurationManager.AppSettings["Facebook_AppId"],
                                        ConfigurationManager.AppSettings["Facebook_AppSecret"],
                                        Url.Encode(RedirectUri.AbsoluteUri),
                                        code));
            ApplicationUserManager UserManager = HttpContext.GetOwinContext().
                GetUserManager<ApplicationUserManager>();
            if (UserManager != null)
            {  
                var userId = HttpContext.User.Identity.GetUserId();
                // Retrieve the existing claims for the user and add the

                IList<Claim> currentClaims = await UserManager.GetClaimsAsync(userId);

                //check to see if a claim already exists for FacebookAccessToken
                Claim OldFacebookAccessTokenClaim = currentClaims.
                    FirstOrDefault(x => x.Type == "FacebookAccessToken");

                //Create new FacebookAccessToken claim
                Claim newFacebookAccessTokenClaim = new Claim("FacebookAccessToken",
                    newTokenResult.access_token);
                if (OldFacebookAccessTokenClaim == null)
                {
                    //Add new FacebookAccessToken Claim
                    await UserManager.AddClaimAsync(userId, newFacebookAccessTokenClaim);
                }
                else
                {
                    //Remove the existing FacebookAccessToken Claim
                    await UserManager.RemoveClaimAsync(userId, OldFacebookAccessTokenClaim);
                    //Add new FacebookAccessToken Claim
                    await UserManager.AddClaimAsync(userId, newFacebookAccessTokenClaim);
                }
                Session.Add("AccessTokenRetryCount", "0");
            }

            return RedirectToAction("Index", "Facebook");
        }


        protected List<FacebookPermissionRequest> CheckPermissions(Dictionary<string, FacebookPermissionRequest> RequiredPermissions)
        {
            var access_token = HttpContext.Items["access_token"].ToString();
            if (!string.IsNullOrEmpty(access_token))
            {
                var appsecret_proof = access_token.GenerateAppSecretProof();
                var fb = new FacebookClient(access_token);

                IEnumerable<FacebookPermissionRequest> MissingPermissions = new List<FacebookPermissionRequest>();  //initialize to an empty list
                if (RequiredPermissions != null)
                {
                    //create an array of Facebook Batch Parameters based on list of RequiredPermission
                    FacebookBatchParameter[] fbBatchParameters = new FacebookBatchParameter[RequiredPermissions.Values.Count()];
                    int idx = 0;
                    foreach (FacebookPermissionRequest required_permssion in RequiredPermissions.Values)
                    {
                        fbBatchParameters[idx] = new FacebookBatchParameter
                        {
                            HttpMethod = HttpMethod.Get,
                            Path = string.Format("{0}{1}", "me/permissions/", required_permssion.permision_scope_value).GraphAPICall(appsecret_proof)
                        };
                        required_permssion.granted = false; //initalize all granted indicators to false for each required permission
                        idx++;
                    }
                    dynamic permission_Batchresult = fb.Batch(fbBatchParameters);

                    if (permission_Batchresult != null)
                    {
                        List<PermissionResults> result = JsonConvert.DeserializeObject<List<PermissionResults>>(permission_Batchresult.ToString());
                        foreach (FacebookPermissionModel permissionResult in result.SelectMany(x => x.data).Where(y => y.status == "granted"))
                        {
                            RequiredPermissions[permissionResult.permission].granted = true;
                        }
                        MissingPermissions = RequiredPermissions.Values.Where(p => p.granted == false);
                    }
                }
                return MissingPermissions.ToList();
            }
            else
                throw new HttpException(404, "Missing Access Token");
        }

        protected string AddPermissions(string permission)
        {
            FacebookClient fb = new FacebookClient();
            fb.AppId = ConfigurationManager.AppSettings["Facebook_AppId"];
            return fb.GetLoginUrl(new
            {
                scope = permission,
                redirect_uri = RedirectUri.AbsoluteUri,
                response_type = "code",
                auth_type = "rerequest"
            }).ToString();
        }
    }
}