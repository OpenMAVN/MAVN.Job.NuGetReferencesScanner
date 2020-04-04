using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace MAVN.Job.NuGetReferencesScanner.Domain
{
    public abstract class GitScannerBase : IReferencesScanner
    {
        private ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> _graph = new ConcurrentDictionary<PackageReference, HashSet<RepoInfo>>();

        protected readonly Timer _timer;

        protected string _status;
        protected DateTime? _lastUpDateTime;
        protected int _scannedProjectFilesCount;
        protected int _foundReposCount;

        protected GitScannerBase()
        {
            _timer = new Timer(_ => ScanAsync().GetAwaiter().GetResult());
        }

        public ScanResult GetScanResult()
        {
            var flatResult = _graph.SelectMany(g => g.Value.Select(v => new Tuple<PackageReference, RepoInfo>(g.Key, v))).ToArray();
            var statString = $"Last update time {_lastUpDateTime}. Found {_foundReposCount} repositories, scanned {_scannedProjectFilesCount} projects.";

            return new ScanResult(_status, statString, flatResult);
        }

        public void Start()
        {
            _timer.Change(TimeSpan.Zero, Timeout.InfiniteTimeSpan);
        }

        protected abstract Task ScanReposAsync(ConcurrentDictionary<PackageReference, HashSet<RepoInfo>> graph);

        private async Task ScanAsync()
        {
            var graph = _graph.Count == 0
                ? _graph
                : new ConcurrentDictionary<PackageReference, HashSet<RepoInfo>>();

            _foundReposCount = 0;
            _scannedProjectFilesCount = 0;

            try
            {
                var scanStart = DateTime.UtcNow;
                _status = $"Scanning. Started at {scanStart:HH:mm:ss}";

                await ScanReposAsync(graph);

                _graph = graph;
                _lastUpDateTime = DateTime.UtcNow;
                Console.WriteLine($"Last scan took {_lastUpDateTime - scanStart}");
                _timer.Change(TimeSpan.FromHours(2), Timeout.InfiniteTimeSpan);
                _status = "Idle";
            }
            catch (Exception ex)
            {
                Console.WriteLine();
                Console.WriteLine($"Error: {ex.Message}");
                int minutesDelay = 20;
                _status = $"Error {DateTime.UtcNow:HH:mm:ss}. Restarting in {minutesDelay} min. {ex.Message}";
                _timer.Change(TimeSpan.FromMinutes(minutesDelay), Timeout.InfiniteTimeSpan);
            }
        }
    }
}
