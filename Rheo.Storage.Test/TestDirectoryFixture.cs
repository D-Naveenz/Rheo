using Rheo.Storage.Test.Models;
using System.Diagnostics;

namespace Rheo.Storage.Test
{
    public class TestDirectoryFixture : IDisposable
    {
        public TestDirectory TestDir { get; }

        public TestDirectoryFixture()
        {
            TestDir = TestDirectory.Create();

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
