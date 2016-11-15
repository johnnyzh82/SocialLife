using SocialLife.Models;
using System.Web.Mvc;

namespace SocialLife.Controllers
{
    public class MessageController : Controller
    {
        // GET: Message
        public ActionResult Index(MessageViewModel _messageViewModel)
        {
            return View(_messageViewModel);
        }
    }
}