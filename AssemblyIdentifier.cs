using DevToys.Api;
using System;
using System.ComponentModel.Composition;
using System.Threading.Tasks;

namespace tesuri.DevToys.XPathTester;

[Export(typeof(IResourceAssemblyIdentifier))]
[Name(nameof(TesuriDevToysXPathTesterAssemblyIdentifier))]
internal sealed class TesuriDevToysXPathTesterAssemblyIdentifier : IResourceAssemblyIdentifier
{
    public ValueTask<FontDefinition[]> GetFontDefinitionsAsync()
    {
        throw new NotImplementedException();
    }
}
