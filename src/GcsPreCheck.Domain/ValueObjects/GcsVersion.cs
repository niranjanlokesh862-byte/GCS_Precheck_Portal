using System.Text.RegularExpressions;

namespace GcsPreCheck.Domain.ValueObjects;

public enum VersionTagTier
{
    Alpha = 0,
    Beta = 1,
    ReleaseCandidate = 2,
    Release = 3,
    Hotfix = 4,
    Patch = 5
}

public readonly struct GcsVersion : IComparable<GcsVersion>, IEquatable<GcsVersion>
{
    private static readonly Regex VersionRegex = new(
        @"^(?<major>\d+)\.(?<minor>\d+)(?:\.(?<build>\d+))?(?:\.(?<revision>\d+))?(?:[_-]?(?<tag>Alpha|Beta|RC|HF|Patch|Hotfix|GA|Release)?(?<tagIndex>\d+)?)?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public int Major { get; }
    public int Minor { get; }
    public int Build { get; }
    public int Revision { get; }
    public VersionTagTier TagTier { get; }
    public int TagIndex { get; }
    public string RawString { get; }

    public GcsVersion(int major, int minor, int build = 0, int revision = 0, VersionTagTier tagTier = VersionTagTier.Release, int tagIndex = 0, string? rawString = null)
    {
        Major = major;
        Minor = minor;
        Build = build;
        Revision = revision;
        TagTier = tagTier;
        TagIndex = tagIndex;
        RawString = rawString ?? $"{major}.{minor}.{build}.{revision}";
    }

    public static bool TryParse(string? input, out GcsVersion version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(input))
            return false;

        var clean = input.Trim();
        var match = VersionRegex.Match(clean);
        if (!match.Success)
            return false;

        var major = int.Parse(match.Groups["major"].Value);
        var minor = int.Parse(match.Groups["minor"].Value);
        var build = match.Groups["build"].Success ? int.Parse(match.Groups["build"].Value) : 0;
        var revision = match.Groups["revision"].Success ? int.Parse(match.Groups["revision"].Value) : 0;

        var tagTier = VersionTagTier.Release;
        var tagIndex = 0;

        if (match.Groups["tag"].Success)
        {
            var tagStr = match.Groups["tag"].Value;
            if (tagStr.Equals("Alpha", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.Alpha;
            else if (tagStr.Equals("Beta", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.Beta;
            else if (tagStr.Equals("RC", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.ReleaseCandidate;
            else if (tagStr.Equals("HF", StringComparison.OrdinalIgnoreCase) || tagStr.Equals("Hotfix", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.Hotfix;
            else if (tagStr.Equals("Patch", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.Patch;
            else if (tagStr.Equals("GA", StringComparison.OrdinalIgnoreCase) || tagStr.Equals("Release", StringComparison.OrdinalIgnoreCase))
                tagTier = VersionTagTier.Release;
        }

        if (match.Groups["tagIndex"].Success)
        {
            tagIndex = int.Parse(match.Groups["tagIndex"].Value);
        }

        version = new GcsVersion(major, minor, build, revision, tagTier, tagIndex, clean);
        return true;
    }

    public static GcsVersion Parse(string input)
    {
        if (!TryParse(input, out var version))
            throw new FormatException($"Input string '{input}' was not in a valid GcsVersion format.");
        return version;
    }

    public int CompareTo(GcsVersion other)
    {
        if (Major != other.Major) return Major.CompareTo(other.Major);
        if (Minor != other.Minor) return Minor.CompareTo(other.Minor);
        if (Build != other.Build) return Build.CompareTo(other.Build);
        if (Revision != other.Revision) return Revision.CompareTo(other.Revision);

        if (TagTier != other.TagTier)
            return TagTier.CompareTo(other.TagTier);

        return TagIndex.CompareTo(other.TagIndex);
    }

    public bool Equals(GcsVersion other) => CompareTo(other) == 0;

    public override bool Equals(object? obj) => obj is GcsVersion other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Build, Revision, (int)TagTier, TagIndex);

    public override string ToString() => RawString;

    public static bool operator ==(GcsVersion left, GcsVersion right) => left.Equals(right);
    public static bool operator !=(GcsVersion left, GcsVersion right) => !left.Equals(right);
    public static bool operator <(GcsVersion left, GcsVersion right) => left.CompareTo(right) < 0;
    public static bool operator <=(GcsVersion left, GcsVersion right) => left.CompareTo(right) <= 0;
    public static bool operator >(GcsVersion left, GcsVersion right) => left.CompareTo(right) > 0;
    public static bool operator >=(GcsVersion left, GcsVersion right) => left.CompareTo(right) >= 0;
}
