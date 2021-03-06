using System;
using System.Collections.Generic;

namespace MAVN.Job.NuGetReferencesScanner.Domain
{
    public sealed class ScanResult
    {
        public string Status { get; }
        public string Statistics { get; }

        public IReadOnlyCollection<Tuple<PackageReference, RepoInfo>> Data { get; }

        public ScanResult(string status, string statistics, IReadOnlyCollection<Tuple<PackageReference, RepoInfo>> data)
        {
            Status = status;
            Statistics = statistics;
            Data = data;
        }
    }
}