namespace GhostAndroid.Core.PlatformTools;

/// <summary>
/// Levée lorsqu'un outil requis (adb/fastboot) est introuvable. L'interface traduit
/// cette erreur en message clair plutôt que de prétendre qu'une opération a réussi.
/// </summary>
public sealed class ToolNotFoundException : Exception
{
    public string Tool { get; }

    public ToolNotFoundException(string tool)
        : base($"Outil introuvable : « {tool} ». Vérifiez l'installation des Android Platform Tools.")
        => Tool = tool;
}
