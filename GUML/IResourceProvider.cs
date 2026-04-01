using Godot;

namespace GUML;

/// <summary>
/// Provides typed resource loading methods for GUML.
/// Each method corresponds to a specific GUML resource function
/// (image(), font(), audio(), video()), eliminating the need
/// for file-extension-based type guessing.
/// </summary>
public interface IResourceProvider
{
    /// <summary>
    /// Loads an image/texture resource from the given path and tracks
    /// the specified node as a consumer of this resource.
    /// Called when GUML encounters <c>image("path")</c>.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="consumer">The Godot node that will use this resource. Used for reference tracking.</param>
    /// <returns>A Godot Texture2D or equivalent image resource.</returns>
    object LoadImage(string path, Node? consumer = null);

    /// <summary>
    /// Loads a font resource from the given path and tracks
    /// the specified node as a consumer of this resource.
    /// Called when GUML encounters <c>font("path")</c>.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="consumer">The Godot node that will use this resource. Used for reference tracking.</param>
    /// <returns>A Godot Font or FontFile resource.</returns>
    object LoadFont(string path, Node? consumer = null);

    /// <summary>
    /// Loads an audio resource from the given path and tracks
    /// the specified node as a consumer of this resource.
    /// Called when GUML encounters <c>audio("path")</c>.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="consumer">The Godot node that will use this resource. Used for reference tracking.</param>
    /// <returns>A Godot AudioStream resource.</returns>
    object LoadAudio(string path, Node? consumer = null);

    /// <summary>
    /// Loads a video resource from the given path and tracks
    /// the specified node as a consumer of this resource.
    /// Called when GUML encounters <c>video("path")</c>.
    /// </summary>
    /// <param name="path">The resource path.</param>
    /// <param name="consumer">The Godot node that will use this resource. Used for reference tracking.</param>
    /// <returns>A Godot VideoStream resource.</returns>
    object LoadVideo(string path, Node? consumer = null);
}
