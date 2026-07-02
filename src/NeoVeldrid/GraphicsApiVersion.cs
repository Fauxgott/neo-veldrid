using System;

namespace NeoVeldrid
{
    /// <summary>
    /// Describes the version number of the underlying graphics API of a <see cref="GraphicsBackend"/>.
    /// </summary>
    public readonly struct GraphicsApiVersion : IEquatable<GraphicsApiVersion>
    {
        /// <summary>
        /// An unknown or invalid version number. Defined as: 0.0.0.0.
        /// </summary>
        public static GraphicsApiVersion Unknown => default;

        /// <summary>
        /// Gets the major version component of the graphics API.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> representing breaking changes, paradigm shifts, or feature overhauls in the
        /// underlying graphics API (e.g., the '4' in OpenGL 4.6).
        /// </value>
        public int Major { get; }

        /// <summary>
        /// Gets the minor version component of the graphics API.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> representing backwards-compatible feature additions or revisions introduced
        /// within the current <see cref="Major"/> lifecycle (e.g., the '6' in OpenGL 4.6).
        /// </value>
        public int Minor { get; }

        /// <summary>
        /// Gets the sub-minor version component of the graphics API.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> representing minor internal revision levels or driver-specific configurations.
        /// </value>
        /// <remarks>When <see cref="GraphicsDevice.GetBackendVersion(GraphicsBackend)"/> is called, this value
        /// is left unset.</remarks>
        public int Subminor { get; }

        /// <summary>
        /// Gets the patch version component of the graphics API.
        /// </summary>
        /// <value>
        /// An <see cref="int"/> representing maintenance revisions, bug fixes, or micro-optimizations that do
        /// not alter the broader public API or introduce brand-new features (e.g. minor Vulkan updates or driver
        /// runtime versions).
        /// </value>
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
        /// Attempts to parse the given OpenGL version string and extracts the version number without specific vendor or API information.
        /// </summary>
        /// <param name="versionString">The OpenGL version string to parse.</param>
        /// <param name="version">The outputted <see cref="GraphicsApiVersion"/> containing the parsed OpenGL version.</param>
        /// <returns>Returns true if the parse succeeded; otherwise false and the outputted <see cref="GraphicsApiVersion"/> will contain nothing.</returns>
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
