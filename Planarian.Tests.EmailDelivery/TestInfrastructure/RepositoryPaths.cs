namespace Planarian.Tests.EmailDelivery.TestInfrastructure;

internal static class RepositoryPaths
{
    public static string Root
    {
        get
        {
            for (var directory = new DirectoryInfo(AppContext.BaseDirectory);
                 directory != null;
                 directory = directory.Parent)
            {
                var dotGit = Path.Combine(directory.FullName, ".git");
                if (File.Exists(dotGit) || Directory.Exists(dotGit))
                    return directory.FullName;
            }

            throw new InvalidOperationException("Could not find the repository root.");
        }
    }
}
