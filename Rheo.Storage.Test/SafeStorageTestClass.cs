using Rheo.Storage.Test.Models;

namespace Rheo.Storage.Test
{
    [Collection("SharedTestDirectoryCollection")]
    public class SafeStorageTestClass
    {
        private readonly TestDirectoryFixture _fixture;

        public SafeStorageTestClass(ITestOutputHelper output, TestDirectoryFixture fixture)
        {
            Output = output;
            _fixture = fixture;

            // Get the current test name
            var testContext = TestContext.Current;
            TestName = testContext.TestCase?.TestMethodName;
            Output.WriteLine("Starting test: " + (TestName ?? "Unknown"));

            // Create a safe directory name by removing invalid characters
            var safeTestName = TestName != null ? string.Concat(TestName.Split(Path.GetInvalidFileNameChars())) : null;
            TestDirectory = _fixture.TestDir.CreateSubdirectory(safeTestName);

            // Log the test-specific directory if it differs from the base directory
            var dirName = TestDirectory.Name;
            if (dirName != TestName)
            {
                Output.WriteLine("Test-specific directory: " + dirName);
            }
        }

        public ITestOutputHelper Output { get; }

        public string? TestName { get; }

        public TestDirectory TestDirectoryParent => _fixture.TestDir;

        public TestDirectory TestDirectory { get; }
    }
}
