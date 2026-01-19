using Rheo.Storage.Extensions;
using System.Diagnostics;

namespace Rheo.Storage.Test
{
    public class TestDirectoryFixture : IDisposable
    {
        public TempDirectory TestDir { get; }

        public TestDirectoryFixture()
        {
            TestDir = TempDirectory.Create();

#if DEBUG
            // Only open the folder when actively debugging (not just running tests)
            if (Debugger.IsAttached)
            {
                TestDir.OpenInFileBrowser();
            }
#endif
        }

        public void Dispose()
        {
            TestDir.Dispose();
            GC.SuppressFinalize(this);
        }
    }

    [CollectionDefinition("SharedTestDirectoryCollection")]
    public class SharedTestDirectoryCollection : ICollectionFixture<TestDirectoryFixture>
    {
        // No code here, just the definition
    }
}
