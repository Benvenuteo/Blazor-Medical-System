using MedicalBookingSystem.Infrastructure;
using Microsoft.Data.Sqlite;

namespace MedicalBookingSystem.IntegrationTests.Database;

public sealed class ConnectionStringTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RelativeDatabasePath_IsSharedByRepositoryRootAndApplication(bool runFromRepositoryRoot)
    {
        var temporaryRoot = Path.Combine(Path.GetTempPath(), "medical-path-tests", Guid.NewGuid().ToString("N"));
        var solutionRoot = Path.Combine(temporaryRoot, "MedicalBookingSystem");
        var applicationRoot = Path.Combine(solutionRoot, "MedicalBookingSystem.WebAPI");
        Directory.CreateDirectory(applicationRoot);
        File.WriteAllText(Path.Combine(solutionRoot, "MedicalBookingSystem.sln"), string.Empty);
        try
        {
            var connectionString = DependencyInjection.ResolveConnectionString(
                "Data Source=data/medical.db", runFromRepositoryRoot ? temporaryRoot : applicationRoot);

            Assert.Equal(Path.Combine(solutionRoot, "data", "medical.db"), new SqliteConnectionStringBuilder(connectionString).DataSource);
        }
        finally
        {
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Fact]
    public void InMemoryDatabase_PreservesSpecialDataSource()
    {
        var result = DependencyInjection.ResolveConnectionString("Data Source=:memory:", Path.GetTempPath());
        Assert.Equal(":memory:", new SqliteConnectionStringBuilder(result).DataSource);
    }
}
