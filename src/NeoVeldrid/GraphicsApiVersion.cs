using System;
using System.Text.RegularExpressions;

namespace NeoVeldrid;

/// <summary>
/// Describes the version number of the underlying graphics API of a <see cref="GraphicsBackend"/>.
/// </summary>
public readonly partial struct GraphicsApiVersion : IEquatable<GraphicsApiVersion>
{
    [GeneratedRegex(@"(?<major>\d+)\.(?<minor>\d+)(?:\.(?<patch>\d+))?")]
    private static partial Regex ParseVersionRegex();

    /// <summary>
    /// An unknown or invalid version number. Defined as: 0.0.0.0.
    /// </summary>
    public static GraphicsApiVersion Unknown => default;

    /// <summary>The major version component (e.g. the <c>4</c> in OpenGL 4.6).</summary>
    public int Major { get; }

    /// <summary>The minor version component (e.g. the <c>6</c> in OpenGL 4.6).</summary>
    public int Minor { get; }

    /// <summary>
    /// The sub-minor version component, between <see cref="Minor"/> and <see cref="Patch"/>. Only a
    /// four-part version (such as a Vulkan conformance version) populates it, so it is typically 0.
    /// </summary>
    public int Subminor { get; }

    /// <summary>
    /// The patch or release version component (e.g. the release number in an OpenGL version string,
    /// or a Vulkan patch level).
    /// </summary>
    public int Patch { get; }

    /// <summary>
    /// Is the version number valid? Defined as: Major > 0, Minor ≥ 0, Subminor ≥ 0, Patch ≥ 0.
    /// </summary>
    public bool IsKnown => Major > 0 && Minor >= 0 && Subminor >= 0 && Patch >= 0;

    public GraphicsApiVersion(int major, int minor, int subminor, int patch)
    {
        Major = major;
        Minor = minor;
        Subminor = subminor;
        Patch = patch;
    }

    public override string ToString()
    {
        return $"{Major}.{Minor}.{Subminor}.{Patch}";
    }

    /// <summary>
    /// Attempts to parse an OpenGL or OpenGL ES version string, extracting the version number and
    /// ignoring any surrounding vendor or API text.
    /// </summary>
    /// <param name="versionString">The version string to parse (e.g. <c>"4.6.0 NVIDIA 551.23"</c>).</param>
    /// <param name="version">The parsed version, or <see cref="Unknown"/> if parsing fails.</param>
    /// <returns><see langword="true"/> if the string was parsed successfully; otherwise <see langword="false"/>.</returns>
    public static bool TryParseGLVersion(string versionString, out GraphicsApiVersion version)
    {
        version = GraphicsApiVersion.Unknown;

        do
        {
            if (string.IsNullOrWhiteSpace(versionString))
                break;

            // OpenGL / OpenGL ES version strings can carry vendor boilerplate around the
            // number, so this Regex pulls out just the version components.
            Match match = ParseVersionRegex().Match(versionString);
            if (!match.Success)
                break;

            int major = int.Parse(match.Groups["major"].Value);
            int minor = int.Parse(match.Groups["minor"].Value);

            // Versions are major.minor[.patch]. The optional third component (an OpenGL release
            // number or a patch level) maps to Patch, leaving Subminor 0 to match how the Vulkan
            // and Direct3D backends construct this struct.
            int patch = 0;
            if (match.Groups["patch"].Success)
                patch = int.Parse(match.Groups["patch"].Value);

            version = new GraphicsApiVersion(major, minor, 0, patch);

            break;
        }
        while (true);

        return version != GraphicsApiVersion.Unknown;
    }

    public override int GetHashCode() => HashCode.Combine(Major, Minor, Subminor, Patch);

    public override bool Equals(object obj) => obj is GraphicsApiVersion && Equals((GraphicsApiVersion)obj);

    public bool Equals(GraphicsApiVersion other)
    {
        if (Major == other.Major && Minor == other.Minor &&
            Subminor == other.Subminor && Patch == other.Patch)
            return true;

        return false;
    }

    public static bool operator ==(GraphicsApiVersion version1, GraphicsApiVersion version2)
        => version1.Equals(version2);
    public static bool operator !=(GraphicsApiVersion version1, GraphicsApiVersion version2)
        => !(version1 == version2);
}
