using NuGet.Client;
using NuGet.Client.VisualStudio;
using NuGet.PackagingCore;
using NuGet.Versioning;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;
using WebAppTest.Models;

namespace WebAppTest.Controllers
{
    public class HomeController : Controller
    {
        public async Task<ActionResult> Index()
        {
            TestModel model = new TestModel();
            model.Result = new List<string>();

            var provider = RepositoryFactory.CreateProvider(new string[] { "https://api.nuget.org/v3/index.json", "https://nuget.org/api/v2" });

            var id = new PackageIdentity("newtonsoft.json", NuGetVersion.Parse("6.0.4"));

            foreach (var source in provider.GetRepositories())
            {
                var resource = await source.GetResourceAsync<DownloadResource>();
                var uri = await resource.GetDownloadUrl(id);
                var stream = await resource.GetStream(id, CancellationToken.None);
            }

            model.Result.Add("DownloadResource");

            foreach (var source in provider.GetRepositories())
            {
                var resource = await source.GetResourceAsync<UISearchResource>();
                var results = await resource.Search("elmah", new SearchFilter(), 0, 10, CancellationToken.None);
            }

            model.Result.Add("UISearchResource");

            return View(model);
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";

            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";

            return View();
        }
    }
}