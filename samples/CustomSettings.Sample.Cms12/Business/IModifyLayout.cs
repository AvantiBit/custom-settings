using AvantiBit.Optimizely.CustomSettings.Sample.Cms12.Models.ViewModels;

namespace AvantiBit.Optimizely.CustomSettings.Sample.Cms12.Business;

/// <summary>
/// Defines a method which may be invoked by PageContextActionFilter allowing controllers
/// to modify common layout properties of the view model.
/// </summary>
internal interface IModifyLayout
{
    void ModifyLayout(LayoutModel layoutModel);
}
