namespace BatchVinci;

/// <summary>Crea (se manca) il collegamento nel menu Start dell'utente.</summary>
public static class StartMenuShortcut
{
    public static void EnsureCreated()
    {
        try
        {
            string? exe = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exe)) return;

            string programs = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Microsoft", "Windows", "Start Menu", "Programs");
            string lnk = Path.Combine(programs, "BatchVinci.lnk");
            if (File.Exists(lnk)) return;

            Directory.CreateDirectory(programs);
            var shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType == null) return;

            dynamic shell = Activator.CreateInstance(shellType)!;
            dynamic link = shell.CreateShortcut(lnk);
            link.TargetPath = exe;
            link.WorkingDirectory = Path.GetDirectoryName(exe);
            link.Description = "BatchVinci";
            string icon = Path.Combine(AppContext.BaseDirectory, "Assets", "AppIcon.ico");
            link.IconLocation = File.Exists(icon) ? icon : exe;
            link.Save();
        }
        catch
        {
            // il collegamento è opzionale: un errore non deve impedire l'avvio
        }
    }
}
