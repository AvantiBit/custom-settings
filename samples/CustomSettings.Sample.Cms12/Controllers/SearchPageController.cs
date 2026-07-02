using AvantiBit.Optimizely.CustomSettings.Sample.Cms12.Models.Pages;
using AvantiBit.Optimizely.CustomSettings.Sample.Cms12.Models.ViewModels;
using Microsoft.AspNetCore.Mvc;

namespace AvantiBit.Optimizely.CustomSettings.Sample.Cms12.Controllers;

public class SearchPageController : PageControllerBase<SearchPage>
{
    public ViewResult Index(SearchPage currentPage, string q)
    {
        var model = new SearchContentModel(currentPage)
        {
            Hits = Enumerable.Empty<SearchContentModel.SearchHit>(),
            NumberOfHits = 0,
            SearchServiceDisabled = true,
            SearchedQuery = q
        };

        return View(model);
    }
}
