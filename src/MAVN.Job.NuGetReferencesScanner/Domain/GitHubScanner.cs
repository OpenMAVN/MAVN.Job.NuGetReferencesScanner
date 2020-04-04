using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Octokit;
using Octokit.Internal;

namespace MAVN.Job.NuGetReferencesScanner.Domain
{
    public sealed class GitHubScanner : GitScannerBase
    {
        private const string ApiKeyEnvVar = "GitHubApiKey";
        private const string OrganizationKeyEnvVar = "GitHubOrganization";

        private readonly GitHubClient _client;
        private readonly HashSet<string> _solutions = new HashSet<string>();
        private readonly string _organization;

        public GitHubScanner(IConfiguration configuration)
        {
            var apiKey = configuration[ApiKeyEnvVar];
            if (string.IsNullOrWhiteSpace(apiKey))
                throw new InvalidOperationException($"{ApiKeyEnvVar} env var can't be empty. For unauthenticated requests rate limit = 60 calls per hour!");
            _organization = configuration[OrganizationKeyEnvVar];
            if (string.IsNullOrWhiteSpace(_organization))
                throw new InvalidOperationException($"{OrganizationKeyEnvVar} env var can't be empty.");

            _client = new GitHubClient(new ProductHeaderValue("MyAmazingApp2"), new InMemoryCredentialStore(new Credentials(apiKey)));
        }

        protected override async Task ScanReposAsync(ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph)
        {
            _solutions.Clear();

            var scr = new SearchCodeRequest("PackageReference Lykke")
            {
                Organization = _organization,
                Extensions = new List<string> { "csproj" }
            };
            var searchResult = await _client.Search.SearchCode(scr);
            var totalProjectsCount = searchResult.TotalCount;

            for (int i = 0; i < totalProjectsCount; i += 100)
            {
                var pageNumber = i / 100;
                scr.Page = pageNumber;

                searchResult = await _client.Search.SearchCode(scr);

                Console.WriteLine($"Page {pageNumber} received {searchResult.Items.Count}");

                foreach (var item in searchResult.Items)
                {
                    _solutions.Add(item.Repository.Name);
                    _foundReposCount = _solutions.Count;

                    await IndexProjectAsync(item, graph);

                    // rate limit - 5000 per hour
                    await Task.Delay(500);
                }
            }
        }

        private async Task IndexProjectAsync(SearchCode repoInfo, ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph)
        {
            var projectContent = await _client.Repository.Content.GetAllContents(repoInfo.Repository.Id, repoInfo.Path);
            var repo = RepoInfo.Parse(repoInfo.Repository.FullName, repoInfo.Repository.HtmlUrl);
            var nugetRefs = ProjectFileParser.Parse(projectContent[0].Content);

            Console.WriteLine($"Repo name {repoInfo.Repository.Name} file name {repoInfo.Name}");

            foreach (var nugetRef in nugetRefs)
            {
                if (!graph.TryGetValue(nugetRef, out var repoInfos))
                    repoInfos = new HashSet<RepoInfo>();

                repoInfos.Add(repo);
                graph[nugetRef] = repoInfos;
            }

            ++_scannedProjectFilesCount;
        }
    }
}
