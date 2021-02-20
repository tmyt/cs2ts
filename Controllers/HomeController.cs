using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Policy;
using System.Threading.Tasks;
using cs2ts;
using cs2ts.Models;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace cs2ts.Controllers
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
            return View(new HomeModel());
        }

        [HttpPost]
        public IActionResult Index(HomeModel model)
        {
            var gen = new Generator();
            model.Generated = gen.Generate(model.Source);
            model.Warnings = new List<string>(gen.Warnings);
            return View(model);
        }

        [HttpPost("/raw")]
        public IActionResult Raw(HomeModel model)
        {
            var gen = new Generator();
            var generated = gen.Generate(model.Source);
            Response.Headers.Add("X-Generator-Warnings", gen.Warnings
                .Select(s => $"\"{s}\"")
                .JoinToString(","));
            return Content(generated, "text/plain");
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
