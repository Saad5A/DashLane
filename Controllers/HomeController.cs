using System.Diagnostics;
using DashLane.Models;
using Microsoft.AspNetCore.Mvc;

namespace DashLane.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            return View();
        }

        [HttpGet]
        public IActionResult SignIn()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("SignIn")]
        public IActionResult SignInPost()
        {
            ViewData["AuthNotice"] = "Authentication is not connected yet.";
            return View("SignIn");
        }

        [HttpGet]
        public IActionResult SignUp()
        {
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [ActionName("SignUp")]
        public IActionResult SignUpPost()
        {
            ViewData["AuthNotice"] = "Account creation is not connected yet.";
            return View("SignUp");
        }

        public IActionResult Community()
        {
            return View();
        }

        public IActionResult AboutYou()
        {
            return View();
        }

        public IActionResult Profile()
        {
            return View();
        }

        public IActionResult Contact()
        {
            return View();
        }

        public IActionResult Identity()
        {
            return View();
        }

        public IActionResult Review()
        {
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }
    }
}
