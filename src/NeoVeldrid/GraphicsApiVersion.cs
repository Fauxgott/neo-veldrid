using System;

namespace NeoVeldrid
{
    public readonly struct GraphicsApiVersion : IEquatable<GraphicsApiVersion>
    {
        public static GraphicsApiVersion Unknown => default;

        public int Major { get; }
        public int Minor { get; }
        public int Subminor { get; }
        public int Patch { get; }

        public bool IsKnown => Major > 0 && Minor > 0 && Subminor >= 0 && Patch >= 0;

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
        /// Parses OpenGL version strings and extracts the version number without specific vendor or API information.
        /// </summary>
        /// <param name="versionString">The OpenGL version string.</param>
        /// <param name="version">The parsed <see cref="GraphicsApiVersion"/>.</param>
        /// <returns>True whether the parse succeeded; otherwise false.</returns>
        [Obsolete("This should not be publicly exposed and will be removed in a future version.")]
        public static bool TryParseGLVersion(string versionString, out GraphicsApiVersion version)
            => OpenGL.OpenGLVersionInfo.TryParseVersionString(versionString, out version);

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
}
