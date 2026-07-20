namespace SwixyPermissionManager.Content;

/// <summary>Сетка + цветовая схема менеджера прав (сине-сланцевая).</summary>
public static class PermissionTheme
{
    public const int UiW = 900;
    public const int UiH = 620;

    public const int Pad = 16;
    public const int Gap = 12;

    public const int LeftX = Pad;
    public const int LeftW = 260;
    public const int ScrollW = 18;

    public const int RightX = LeftX + LeftW + ScrollW + Gap; // 306
    public const int RightW = UiW - RightX - Pad - ScrollW;

    public const int HeaderH = 28;
    public const int RowH = 36;
    public const int InputH = 28;
    public const int BtnH = 32;
    public const int BtnW = 100;

    // ---- Palette (navy / slate / accent) ----
    /// <summary>Deep navy panel fill.</summary>
    public static readonly double[] ColNavy = [0.09, 0.12, 0.20, 1.0];
    /// <summary>Slightly lighter well (lists).</summary>
    public static readonly double[] ColWell = [0.11, 0.15, 0.24, 0.96];
    /// <summary>Card face (role row).</summary>
    public static readonly double[] ColCard = [0.14, 0.18, 0.28, 0.96];
    /// <summary>Selected card / accent amber-blue mix.</summary>
    public static readonly double[] ColCardSelected = [0.18, 0.28, 0.42, 0.98];
    /// <summary>Privilege granted row.</summary>
    public static readonly double[] ColGranted = [0.12, 0.28, 0.22, 0.96];
    /// <summary>Privilege denied/off row.</summary>
    public static readonly double[] ColDenied = [0.12, 0.14, 0.20, 0.94];
    /// <summary>Hover wash.</summary>
    public static readonly double[] ColHover = [1, 1, 1, 0.07];
    /// <summary>Border soft blue-gray.</summary>
    public static readonly double[] ColBorder = [0.28, 0.38, 0.52, 0.85];
    /// <summary>Title / cream text.</summary>
    public static readonly double[] ColText = [0.90, 0.93, 0.98, 1.0];
    /// <summary>Muted secondary text.</summary>
    public static readonly double[] ColTextMuted = [0.62, 0.70, 0.82, 1.0];
    /// <summary>Accent cyan for icons/headers.</summary>
    public static readonly double[] ColAccent = [0.31, 0.71, 0.88, 1.0]; // #50B5E1
    /// <summary>Success green.</summary>
    public static readonly double[] ColOk = [0.35, 0.82, 0.55, 1.0];
    /// <summary>Danger red.</summary>
    public static readonly double[] ColDanger = [0.99, 0.35, 0.33, 1.0]; // #FD5A53
    /// <summary>Warn amber.</summary>
    public static readonly double[] ColWarn = [0.92, 0.70, 0.22, 1.0];
    /// <summary>Bottom detail panel.</summary>
    public static readonly double[] ColDetailPanel = [0.07, 0.11, 0.20, 0.97];
    public static readonly double[] ColDetailBorder = [0.22, 0.36, 0.55, 1.0];
    public static readonly double[] ColDetailText = [0.84, 0.90, 0.98, 1.0];
    /// <summary>Input field background.</summary>
    public static readonly double[] ColInput = [0.08, 0.11, 0.18, 0.95];
}
