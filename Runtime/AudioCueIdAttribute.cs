using System.Runtime.CompilerServices;
using UnityEngine;

[assembly: InternalsVisibleTo("kinatraa.AudioSystem.Editor")]

namespace kinatraa.AudioSystem
{
    /// <summary>
    /// Put on a string field to pick a cue id from a searchable dropdown of every <see cref="AudioLibrary"/> in the project.
    /// </summary>
    public sealed class AudioCueIdAttribute : PropertyAttribute
    {
    }
}
