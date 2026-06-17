using AvantiBit.Optimizely.CustomSettings.Sample.Cms13.Models.Pages;
using EPiServer.Shell;

namespace AvantiBit.Optimizely.CustomSettings.Sample.Cms13.Business.UIDescriptors;

/// <summary>
/// Describes how the UI should appear for <see cref="ContainerPage"/> content.
/// </summary>
[UIDescriptorRegistration]
public class ContainerPageUIDescriptor : UIDescriptor<ContainerPage>
{
    public ContainerPageUIDescriptor()
        : base(ContentTypeCssClassNames.Container)
    {
        DefaultView = CmsViewNames.AllPropertiesView;
    }
}
