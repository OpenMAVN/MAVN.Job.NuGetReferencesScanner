namespace MAVN.Job.NuGetReferencesScanner.Domain
{
    public interface IReferencesScanner
    {
        ScanResult GetScanResult();
        void Start();
    }
}
