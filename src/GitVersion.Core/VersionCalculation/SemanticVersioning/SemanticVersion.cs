using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.RegularExpressions;
using GitVersion.Extensions;

namespace GitVersion;

public class SemanticVersion : IFormattable, IComparable<SemanticVersion>, IEquatable<SemanticVersion?>
{
    public static readonly SemanticVersion Empty = new();

    // uses the git-semver spec https://github.com/semver/semver/blob/master/semver.md
    private static readonly Regex ParseSemVerStrict = new(
        @"^(?<major>0|[1-9]\d*)\.(?<minor>0|[1-9]\d*)\.(?<patch>0|[1-9]\d*)(?:-(?<prerelease>(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*)(?:\.(?:0|[1-9]\d*|\d*[a-zA-Z-][0-9a-zA-Z-]*))*))?(?:\+(?<buildmetadata>[0-9a-zA-Z-]+(?:\.[0-9a-zA-Z-]+)*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ParseSemVerLoose = new(
        @"^(?<SemVer>(?<Major>\d+)(\.(?<Minor>\d+))?(\.(?<Patch>\d+))?)(\.(?<FourthPart>\d+))?(-(?<Tag>[^\+]*))?(\+(?<BuildMetaData>.*))?$",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public long Major;
    public long Minor;
    public long Patch;
    public SemanticVersionPreReleaseTag PreReleaseTag;
    public SemanticVersionBuildMetaData BuildMetaData;

    public bool IsLabeledWith(string value) => PreReleaseTag.HasTag() && PreReleaseTag.Name.IsEquivalentTo(value);

    public bool IsMatchForBranchSpecificLabel(string? value)
        => PreReleaseTag.Name == string.Empty || value is null || IsLabeledWith(value);

    public SemanticVersion(long major = 0, long minor = 0, long patch = 0)
    {
        this.Major = major;
        this.Minor = minor;
        this.Patch = patch;
        this.PreReleaseTag = new SemanticVersionPreReleaseTag();
        this.BuildMetaData = new SemanticVersionBuildMetaData();
    }

    public SemanticVersion(SemanticVersion semanticVersion)
    {
        semanticVersion.NotNull();

        this.Major = semanticVersion.Major;
        this.Minor = semanticVersion.Minor;
        this.Patch = semanticVersion.Patch;

        this.PreReleaseTag = new SemanticVersionPreReleaseTag(semanticVersion.PreReleaseTag);
        this.BuildMetaData = new SemanticVersionBuildMetaData(semanticVersion.BuildMetaData);
    }

    public bool Equals(SemanticVersion? obj)
    {
        if (obj == null)
        {
            return false;
        }
        return this.Major == obj.Major && this.Minor == obj.Minor && this.Patch == obj.Patch && this.PreReleaseTag == obj.PreReleaseTag && this.BuildMetaData == obj.BuildMetaData;
    }

    public bool IsEmpty() => Equals(Empty);

    public override bool Equals(object? obj)
    {
        if (obj is null)
        {
            return false;
        }
        if (ReferenceEquals(this, obj))
        {
            return true;
        }
        return obj.GetType() == GetType() && Equals((SemanticVersion)obj);
    }

    public override int GetHashCode()
    {
        unchecked
        {
            var hashCode = this.Major.GetHashCode();
            hashCode = (hashCode * 397) ^ this.Minor.GetHashCode();
            hashCode = (hashCode * 397) ^ this.Patch.GetHashCode();
            hashCode = (hashCode * 397) ^ this.PreReleaseTag.GetHashCode();
            hashCode = (hashCode * 397) ^ this.BuildMetaData.GetHashCode();
            return hashCode;
        }
    }

    public static bool operator ==(SemanticVersion? v1, SemanticVersion? v2)
    {
        if (v1 is null)
        {
            return v2 is null;
        }
        return v1.Equals(v2);
    }

    public static bool operator !=(SemanticVersion? v1, SemanticVersion? v2) => !(v1 == v2);

    public static bool operator >(SemanticVersion v1, SemanticVersion v2)
    {
        if (v1 == null)
            throw new ArgumentNullException(nameof(v1));
        if (v2 == null)
            throw new ArgumentNullException(nameof(v2));

        return v1.CompareTo(v2) > 0;
    }

    public static bool operator >=(SemanticVersion v1, SemanticVersion v2)
    {
        if (v1 == null)
            throw new ArgumentNullException(nameof(v1));
        if (v2 == null)
            throw new ArgumentNullException(nameof(v2));

        return v1.CompareTo(v2) >= 0;
    }

    public static bool operator <=(SemanticVersion v1, SemanticVersion v2)
    {
        if (v1 == null)
            throw new ArgumentNullException(nameof(v1));
        if (v2 == null)
            throw new ArgumentNullException(nameof(v2));

        return v1.CompareTo(v2) <= 0;
    }

    public static bool operator <(SemanticVersion v1, SemanticVersion v2)
    {
        if (v1 == null)
            throw new ArgumentNullException(nameof(v1));
        if (v2 == null)
            throw new ArgumentNullException(nameof(v2));

        return v1.CompareTo(v2) < 0;
    }

    public static SemanticVersion Parse(string version, string? tagPrefixRegex, SemanticVersionFormat versionFormat = SemanticVersionFormat.Strict)
    {
        if (!TryParse(version, tagPrefixRegex, out var semanticVersion, versionFormat))
            throw new WarningException($"Failed to parse {version} into a Semantic Version");

        return semanticVersion;
    }

    public static bool TryParse(string version, string? tagPrefixRegex, [NotNullWhen(true)] out SemanticVersion? semanticVersion, SemanticVersionFormat format = SemanticVersionFormat.Strict)
    {
        var match = Regex.Match(version, $"^({tagPrefixRegex})(?<version>.*)$");

        if (!match.Success)
        {
            semanticVersion = null;
            return false;
        }

        version = match.Groups["version"].Value;
        return format == SemanticVersionFormat.Strict
            ? TryParseStrict(version, out semanticVersion)
            : TryParseLoose(version, out semanticVersion);
    }

    private static bool TryParseStrict(string version, [NotNullWhen(true)] out SemanticVersion? semanticVersion)
    {
        var parsed = ParseSemVerStrict.Match(version);

        if (!parsed.Success)
        {
            semanticVersion = null;
            return false;
        }

        semanticVersion = new SemanticVersion
        {
            Major = long.Parse(parsed.Groups["major"].Value),
            Minor = parsed.Groups["minor"].Success ? long.Parse(parsed.Groups["minor"].Value) : 0,
            Patch = parsed.Groups["patch"].Success ? long.Parse(parsed.Groups["patch"].Value) : 0,
            PreReleaseTag = SemanticVersionPreReleaseTag.Parse(parsed.Groups["prerelease"].Value),
            BuildMetaData = SemanticVersionBuildMetaData.Parse(parsed.Groups["buildmetadata"].Value)
        };

        return true;
    }

    private static bool TryParseLoose(string version, [NotNullWhen(true)] out SemanticVersion? semanticVersion)
    {
        var parsed = ParseSemVerLoose.Match(version);

        if (!parsed.Success)
        {
            semanticVersion = null;
            return false;
        }

        var semanticVersionBuildMetaData = SemanticVersionBuildMetaData.Parse(parsed.Groups["BuildMetaData"].Value);
        var fourthPart = parsed.Groups["FourthPart"];
        if (fourthPart.Success && semanticVersionBuildMetaData.CommitsSinceTag == null)
        {
            semanticVersionBuildMetaData.CommitsSinceTag = int.Parse(fourthPart.Value);
        }

        semanticVersion = new SemanticVersion
        {
            Major = long.Parse(parsed.Groups["Major"].Value),
            Minor = parsed.Groups["Minor"].Success ? long.Parse(parsed.Groups["Minor"].Value) : 0,
            Patch = parsed.Groups["Patch"].Success ? long.Parse(parsed.Groups["Patch"].Value) : 0,
            PreReleaseTag = SemanticVersionPreReleaseTag.Parse(parsed.Groups["Tag"].Value),
            BuildMetaData = semanticVersionBuildMetaData
        };

        return true;
    }

    public int CompareTo(SemanticVersion? value) => CompareTo(value, true);

    public int CompareTo(SemanticVersion? value, bool includePrerelease)
    {
        if (value == null)
        {
            return 1;
        }
        if (this.Major != value.Major)
        {
            if (this.Major > value.Major)
            {
                return 1;
            }
            return -1;
        }
        if (this.Minor != value.Minor)
        {
            if (this.Minor > value.Minor)
            {
                return 1;
            }
            return -1;
        }
        if (this.Patch != value.Patch)
        {
            if (this.Patch > value.Patch)
            {
                return 1;
            }
            return -1;
        }
        if (includePrerelease && this.PreReleaseTag != value.PreReleaseTag)
        {
            if (this.PreReleaseTag > value.PreReleaseTag)
            {
                return 1;
            }
            return -1;
        }

        return 0;
    }

    public override string ToString() => ToString("s");

    public string ToString(string format) => ToString(format, CultureInfo.CurrentCulture);

    /// <summary>
    /// <para>s - Default SemVer [1.2.3-beta.4]</para>
    /// <para>f - Full SemVer [1.2.3-beta.4+5]</para>
    /// <para>i - Informational SemVer [1.2.3-beta.4+5.Branch.main.BranchType.main.Sha.000000]</para>
    /// <para>j - Just the SemVer part [1.2.3]</para>
    /// <para>t - SemVer with the tag [1.2.3-beta.4]</para>
    /// </summary>
    public string ToString(string? format, IFormatProvider? formatProvider)
    {
        if (format.IsNullOrEmpty())
            format = "s";

        if (formatProvider?.GetFormat(GetType()) is ICustomFormatter formatter)
            return formatter.Format(format, this, formatProvider);

        // Check for lp first because the param can vary
        format = format.ToLower();
        switch (format)
        {
            case "j":
                return $"{this.Major}.{this.Minor}.{this.Patch}";
            case "s":
                return this.PreReleaseTag.HasTag() ? $"{ToString("j")}-{this.PreReleaseTag}" : ToString("j");
            case "t":
                return this.PreReleaseTag.HasTag() ? $"{ToString("j")}-{this.PreReleaseTag.ToString("t")}" : ToString("j");
            case "f":
                {
                    var buildMetadata = this.BuildMetaData.ToString();

                    return !buildMetadata.IsNullOrEmpty() ? $"{ToString("s")}+{buildMetadata}" : ToString("s");
                }
            case "i":
                {
                    var buildMetadata = this.BuildMetaData.ToString("f");

                    return !buildMetadata.IsNullOrEmpty() ? $"{ToString("s")}+{buildMetadata}" : ToString("s");
                }
            default:
                throw new FormatException($"Unknown format '{format}'.");
        }
    }

    public SemanticVersion IncrementVersion(string? label, params VersionField[] incrementStrategy)
    {
        var incremented = new SemanticVersion(this);

        foreach (var item in incrementStrategy)
        {
            if (!incremented.PreReleaseTag.HasTag())
            {
                switch (item)
                {
                    case VersionField.None:
                        break;
                    case VersionField.Major:
                        incremented.Major++;
                        incremented.Minor = 0;
                        incremented.Patch = 0;
                        break;
                    case VersionField.Minor:
                        incremented.Minor++;
                        incremented.Patch = 0;
                        break;
                    case VersionField.Patch:
                        incremented.Patch++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(incrementStrategy));
                }
            }
        }

        if (incremented.PreReleaseTag.HasTag() && incremented.PreReleaseTag.Number != null)
        {
            incremented.PreReleaseTag.Number++;
        }

        if (!PreReleaseTag.HasTag() && !label.IsNullOrEmpty())
        {
            incremented.PreReleaseTag = new SemanticVersionPreReleaseTag(label, 1);
        }

        return incremented;
    }

    public SemanticVersion IncrementVersion(bool isContinuousDeployment, string? label, params VersionField[] incrementStrategy)
    {
        var incremented = new SemanticVersion(this);

        foreach (var item in incrementStrategy)
        {
            if (!incremented.PreReleaseTag.HasTag())
            {
                switch (item)
                {
                    case VersionField.None:
                        break;
                    case VersionField.Major:
                        incremented.Major++;
                        incremented.Minor = 0;
                        incremented.Patch = 0;
                        break;
                    case VersionField.Minor:
                        incremented.Minor++;
                        incremented.Patch = 0;
                        break;
                    case VersionField.Patch:
                        incremented.Patch++;
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(incrementStrategy));
                }
            }
        }

        if (incremented.PreReleaseTag.HasTag() && incremented.PreReleaseTag.Number != null)
        {
            incremented.PreReleaseTag.Number++;
        }

        if (!PreReleaseTag.HasTag() && !label.IsNullOrEmpty())
        {
            incremented.PreReleaseTag = new(label, isContinuousDeployment ? 1 : 1);
        }

        return incremented;
    }
}
