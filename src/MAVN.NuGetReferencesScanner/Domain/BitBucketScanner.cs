using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using SharpBucket;
using SharpBucket.V2;
using SharpBucket.V2.Pocos;

namespace Lykke.NuGetReferencesScanner.Domain
{
    public class BitBucketScanner : GitScannerBase
    {
        private const string KeyEnvVar = "BitBucketKey";
        private const string SecretEnvVar = "BitBucketSecret";

        private readonly SharpBucketV2 _client;
        private readonly string _bbAccount;
        private readonly Dictionary<string, RepoInfo> _reposCache = new Dictionary<string, RepoInfo>();

        internal const string AccountEnvVar = "BitBucketAccount";

        public BitBucketScanner(IConfiguration configuration)
        {
            var bbKey = configuration[KeyEnvVar];
            if (string.IsNullOrWhiteSpace(bbKey))
                throw new InvalidOperationException($"{KeyEnvVar} env var can't be empty!");
            var bbSecret = configuration[SecretEnvVar];
            if (string.IsNullOrWhiteSpace(bbSecret))
                throw new InvalidOperationException($"{SecretEnvVar} env var can't be empty!");
            _bbAccount = configuration[AccountEnvVar];
            if (string.IsNullOrWhiteSpace(_bbAccount))
                throw new InvalidOperationException($"{AccountEnvVar} env var can't be empty!");

            _client = new SharpBucketV2();
            _client.OAuth2ClientCredentials(bbKey, bbSecret);
        }

        protected override Task ScanReposAsync(ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph)
        {
            _foundReposCount = _reposCache.Count;

            var teamResource = _client.TeamsEndPoint().TeamResource(_bbAccount);
            var searchResults = teamResource.EnumerateSearchCodeSearchResults("PackageReference");
            foreach (var searchResult in searchResults)
            {
                IndexProject(searchResult, graph);
            }

            return Task.CompletedTask;
        }

        private void IndexProject(
            SearchCodeSearchResult projectFile,
            ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph)
        {
            int retryCount = 0;
            while (retryCount < 10)
            {
                try
                {
                    IndexProject(projectFile.file, graph);
                    break;
                }
                catch (BitbucketException)
                {
                    ++retryCount;
                    //To reduce calls rate limit
                    Thread.Sleep(TimeSpan.FromSeconds(5 * retryCount));
                }
            }
        }

        private void IndexProject(SrcFileInfo searchFile, ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph)
        {
            var fileName = Path.GetFileName(searchFile.path);
            if (!fileName.EndsWith(".csproj"))
                return;

            var repoSlug = ExtractSlugFromUrl(searchFile.links.self.href);
            var repoResource = _client.RepositoriesEndPoint().RepositoryResource(_bbAccount, repoSlug);
            if (!_reposCache.TryGetValue(repoSlug, out var repo))
            {
                var repoInfo = repoResource.GetRepository();
                repo = RepoInfo.Parse(repoInfo.slug, repoInfo.links.html.href);
                _reposCache.Add(repoSlug, repo);
                ++_foundReposCount;
            }

            var projectContent = repoResource.SrcResource().GetFileContent(searchFile.path);
            var nugetRefs = ProjectFileParser.Parse(projectContent);

            foreach (var nugetRef in nugetRefs)
            {
                if (!graph.TryGetValue(nugetRef, out var repoInfos))
                    repoInfos = new HashSet<RepoInfo>();

                repoInfos.Add(repo);
                graph[nugetRef] = repoInfos;
            }

            ++_scannedProjectFilesCount;

            Console.WriteLine($"Processed {fileName} from {repo.Name}");
        }

        private string ExtractSlugFromUrl(string url)
        {
            var parts = url.Split('/', StringSplitOptions.RemoveEmptyEntries);
            for (int i = 1; i < parts.Length; i++)
            {
                if (parts[i] == "src")
                    return parts[i - 1];
            }

            throw new ArgumentException($"Coudln't extract repo sluf from url {url}");
        }
    }
}
