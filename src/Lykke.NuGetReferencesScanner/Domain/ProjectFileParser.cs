using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Lykke.NuGetReferencesScanner.Domain
{
    public static class ProjectFileParser
    {
        private const string Pattern = "<PackageReference\\s+Include\\s*=\\s*\\\"(Lykke.+|Falcon.+)\\\"\\s+Version\\s*=\\s*\"(.+)\\\"";
        private static readonly Regex Regex = new Regex(Pattern);


        public static IReadOnlyCollection<PackageReference> Parse(string projectFileContent)
        {
            var matches = Regex.Matches(projectFileContent);
            var result = matches.Select(m => PackageReference.Parse(m.Groups[1].Value, m.Groups[2].Value))
                .ToArray();

            return result;
        }
    }
}
