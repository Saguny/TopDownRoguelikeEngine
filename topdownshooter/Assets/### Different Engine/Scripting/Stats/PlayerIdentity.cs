using System;
using System.IO;
using UnityEngine;

// who is playing: the name of their user folder (C:/Users/<name> on Windows, the home folder
// elsewhere), so the menu greets whoever's computer it is instead of whoever made the build
public static class PlayerIdentity
{
    private static string cached;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ResetStatics() => cached = null;

    public static string Name
    {
        get
        {
            if (cached == null) cached = Find();
            return cached;
        }
    }

    private static string Find()
    {
        // the profile folder's own name, e.g. C:/Users/theirname
        try
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            string folder = string.IsNullOrEmpty(profile) ? null : Path.GetFileName(profile.TrimEnd('/', '\\'));
            if (!string.IsNullOrWhiteSpace(folder)) return folder;
        }
        catch (Exception) { }

        // the account name, when the profile folder can't be read
        try
        {
            if (!string.IsNullOrWhiteSpace(Environment.UserName)) return Environment.UserName;
        }
        catch (Exception) { }

        return "player";
    }
}
