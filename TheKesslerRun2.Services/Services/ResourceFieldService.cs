using TheKesslerRun2.Services.Model;

namespace TheKesslerRun2.Services.Services;
internal class ResourceFieldService
{
    internal static ResourceFieldService Instance { get; } = new ResourceFieldService();

    private ResourceFieldService() { }

    private List<ResourceField> _resourceFields = [];

    public ResourceField? GetResourceFieldById(Guid id) => _resourceFields.FirstOrDefault(rf => rf.Id == id);
}
