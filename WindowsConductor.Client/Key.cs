namespace WindowsConductor.Client;

#pragma warning disable CA1069 // Enums should not have duplicate values
#pragma warning disable CA1720 // Identifiers should not contain type names

/// <summary>
/// This is basically a copy of FlaUI's VirtualKeyShort, but the values are for reference only, not to be used in any
/// context.
/// </summary>
public enum Key : ushort
{
    /// <summary>Left mouse button</summary>
    LBUTTON = 1,
    /// <summary>Right mouse button</summary>
    RBUTTON = 2,
    /// <summary>Control-break processing</summary>
    CANCEL = 3,
    /// <summary>Middle mouse button (three-button mouse)</summary>
    MBUTTON = 4,
    /// <summary>Windows 2000/XP: X1 mouse button</summary>
    XBUTTON1 = 5,
    /// <summary>Windows 2000/XP: X2 mouse button</summary>
    XBUTTON2 = 6,
    /// <summary>BACKSPACE key</summary>
    BACK = 8,
    /// <summary>TAB key</summary>
    TAB = 9,
    /// <summary>CLEAR key</summary>
    CLEAR = 12, // 0x000C
    /// <summary>ENTER key</summary>
    ENTER = 13, // 0x000D
    /// <summary>SHIFT key</summary>
    SHIFT = 16, // 0x0010
    /// <summary>CTRL key</summary>
    CONTROL = 17, // 0x0011
    /// <summary>ALT key</summary>
    ALT = 18, // 0x0012
    /// <summary>PAUSE key</summary>
    PAUSE = 19, // 0x0013
    /// <summary>CAPS LOCK key</summary>
    CAPSLOCK = 20, // 0x0014
    /// <summary>IME Hangul mode</summary>
    HANGUL = 21, // 0x0015
    /// <summary>Input Method Editor (IME) Kana mode</summary>
    KANA = 21, // 0x0015
    /// <summary>IME Junja mode</summary>
    JUNJA = 23, // 0x0017
    /// <summary>IME final mode</summary>
    FINAL = 24, // 0x0018
    /// <summary>IME Hanja mode</summary>
    HANJA = 25, // 0x0019
    /// <summary>IME Kanji mode</summary>
    KANJI = 25, // 0x0019
    /// <summary>ESC key</summary>
    ESCAPE = 27, // 0x001B
    /// <summary>IME convert</summary>
    CONVERT = 28, // 0x001C
    /// <summary>IME nonconvert</summary>
    NONCONVERT = 29, // 0x001D
    /// <summary>IME accept</summary>
    ACCEPT = 30, // 0x001E
    /// <summary>IME mode change request</summary>
    MODECHANGE = 31, // 0x001F
    /// <summary>SPACEBAR</summary>
    SPACE = 32, // 0x0020
    /// <summary>PAGE UP key</summary>
    PRIOR = 33, // 0x0021
    /// <summary>PAGE DOWN key</summary>
    NEXT = 34, // 0x0022
    /// <summary>END key</summary>
    END = 35, // 0x0023
    /// <summary>HOME key</summary>
    HOME = 36, // 0x0024
    /// <summary>LEFT ARROW key</summary>
    LEFT = 37, // 0x0025
    /// <summary>UP ARROW key</summary>
    UP = 38, // 0x0026
    /// <summary>RIGHT ARROW key</summary>
    RIGHT = 39, // 0x0027
    /// <summary>DOWN ARROW key</summary>
    DOWN = 40, // 0x0028
    /// <summary>SELECT key</summary>
    SELECT = 41, // 0x0029
    /// <summary>PRINT key</summary>
    PRINT = 42, // 0x002A
    /// <summary>EXECUTE key</summary>
    EXECUTE = 43, // 0x002B
    /// <summary>PRINT SCREEN key</summary>
    SNAPSHOT = 44, // 0x002C
    /// <summary>INS key</summary>
    INSERT = 45, // 0x002D
    /// <summary>DEL key</summary>
    DELETE = 46, // 0x002E
    /// <summary>HELP key</summary>
    HELP = 47, // 0x002F
    /// <summary>0 key</summary>
    KEY_0 = 48, // 0x0030
    /// <summary>1 key</summary>
    KEY_1 = 49, // 0x0031
    /// <summary>2 key</summary>
    KEY_2 = 50, // 0x0032
    /// <summary>3 key</summary>
    KEY_3 = 51, // 0x0033
    /// <summary>4 key</summary>
    KEY_4 = 52, // 0x0034
    /// <summary>5 key</summary>
    KEY_5 = 53, // 0x0035
    /// <summary>6 key</summary>
    KEY_6 = 54, // 0x0036
    /// <summary>7 key</summary>
    KEY_7 = 55, // 0x0037
    /// <summary>8 key</summary>
    KEY_8 = 56, // 0x0038
    /// <summary>9 key</summary>
    KEY_9 = 57, // 0x0039
    /// <summary>A key</summary>
    KEY_A = 65, // 0x0041
    /// <summary>B key</summary>
    KEY_B = 66, // 0x0042
    /// <summary>C key</summary>
    KEY_C = 67, // 0x0043
    /// <summary>D key</summary>
    KEY_D = 68, // 0x0044
    /// <summary>E key</summary>
    KEY_E = 69, // 0x0045
    /// <summary>F key</summary>
    KEY_F = 70, // 0x0046
    /// <summary>G key</summary>
    KEY_G = 71, // 0x0047
    /// <summary>H key</summary>
    KEY_H = 72, // 0x0048
    /// <summary>I key</summary>
    KEY_I = 73, // 0x0049
    /// <summary>J key</summary>
    KEY_J = 74, // 0x004A
    /// <summary>K key</summary>
    KEY_K = 75, // 0x004B
    /// <summary>L key</summary>
    KEY_L = 76, // 0x004C
    /// <summary>M key</summary>
    KEY_M = 77, // 0x004D
    /// <summary>N key</summary>
    KEY_N = 78, // 0x004E
    /// <summary>O key</summary>
    KEY_O = 79, // 0x004F
    /// <summary>P key</summary>
    KEY_P = 80, // 0x0050
    /// <summary>Q key</summary>
    KEY_Q = 81, // 0x0051
    /// <summary>R key</summary>
    KEY_R = 82, // 0x0052
    /// <summary>S key</summary>
    KEY_S = 83, // 0x0053
    /// <summary>T key</summary>
    KEY_T = 84, // 0x0054
    /// <summary>U key</summary>
    KEY_U = 85, // 0x0055
    /// <summary>V key</summary>
    KEY_V = 86, // 0x0056
    /// <summary>W key</summary>
    KEY_W = 87, // 0x0057
    /// <summary>X key</summary>
    KEY_X = 88, // 0x0058
    /// <summary>Y key</summary>
    KEY_Y = 89, // 0x0059
    /// <summary>Z key</summary>
    KEY_Z = 90, // 0x005A
    /// <summary>Left Windows key (Microsoft Natural keyboard)</summary>
    LWIN = 91, // 0x005B
    /// <summary>Right Windows key (Natural keyboard)</summary>
    RWIN = 92, // 0x005C
    /// <summary>Applications key (Natural keyboard)</summary>
    APPS = 93, // 0x005D
    /// <summary>Computer Sleep key</summary>
    SLEEP = 95, // 0x005F
    /// <summary>Numeric keypad 0 key</summary>
    NUMPAD0 = 96, // 0x0060
    /// <summary>Numeric keypad 1 key</summary>
    NUMPAD1 = 97, // 0x0061
    /// <summary>Numeric keypad 2 key</summary>
    NUMPAD2 = 98, // 0x0062
    /// <summary>Numeric keypad 3 key</summary>
    NUMPAD3 = 99, // 0x0063
    /// <summary>Numeric keypad 4 key</summary>
    NUMPAD4 = 100, // 0x0064
    /// <summary>Numeric keypad 5 key</summary>
    NUMPAD5 = 101, // 0x0065
    /// <summary>Numeric keypad 6 key</summary>
    NUMPAD6 = 102, // 0x0066
    /// <summary>Numeric keypad 7 key</summary>
    NUMPAD7 = 103, // 0x0067
    /// <summary>Numeric keypad 8 key</summary>
    NUMPAD8 = 104, // 0x0068
    /// <summary>Numeric keypad 9 key</summary>
    NUMPAD9 = 105, // 0x0069
    /// <summary>Multiply key</summary>
    MULTIPLY = 106, // 0x006A
    /// <summary>Add key</summary>
    ADD = 107, // 0x006B
    /// <summary>Separator key</summary>
    SEPARATOR = 108, // 0x006C
    /// <summary>Subtract key</summary>
    SUBTRACT = 109, // 0x006D
    /// <summary>Decimal key</summary>
    DECIMAL = 110, // 0x006E
    /// <summary>Divide key</summary>
    DIVIDE = 111, // 0x006F
    /// <summary>F1 key</summary>
    F1 = 112, // 0x0070
    /// <summary>F2 key</summary>
    F2 = 113, // 0x0071
    /// <summary>F3 key</summary>
    F3 = 114, // 0x0072
    /// <summary>F4 key</summary>
    F4 = 115, // 0x0073
    /// <summary>F5 key</summary>
    F5 = 116, // 0x0074
    /// <summary>F6 key</summary>
    F6 = 117, // 0x0075
    /// <summary>F7 key</summary>
    F7 = 118, // 0x0076
    /// <summary>F8 key</summary>
    F8 = 119, // 0x0077
    /// <summary>F9 key</summary>
    F9 = 120, // 0x0078
    /// <summary>F10 key</summary>
    F10 = 121, // 0x0079
    /// <summary>F11 key</summary>
    F11 = 122, // 0x007A
    /// <summary>F12 key</summary>
    F12 = 123, // 0x007B
    /// <summary>F13 key</summary>
    F13 = 124, // 0x007C
    /// <summary>F14 key</summary>
    F14 = 125, // 0x007D
    /// <summary>F15 key</summary>
    F15 = 126, // 0x007E
    /// <summary>F16 key</summary>
    F16 = 127, // 0x007F
    /// <summary>F17 key</summary>
    F17 = 128, // 0x0080
    /// <summary>F18 key</summary>
    F18 = 129, // 0x0081
    /// <summary>F19 key</summary>
    F19 = 130, // 0x0082
    /// <summary>F20 key</summary>
    F20 = 131, // 0x0083
    /// <summary>F21 key</summary>
    F21 = 132, // 0x0084
    /// <summary>F22 key, (PPC only) Key used to lock device.</summary>
    F22 = 133, // 0x0085
    /// <summary>F23 key</summary>
    F23 = 134, // 0x0086
    /// <summary>F24 key</summary>
    F24 = 135, // 0x0087
    /// <summary>NUM LOCK key</summary>
    NUMLOCK = 144, // 0x0090
    /// <summary>SCROLL LOCK key</summary>
    SCROLL = 145, // 0x0091
    /// <summary>Left SHIFT key</summary>
    LSHIFT = 160, // 0x00A0
    /// <summary>Right SHIFT key</summary>
    RSHIFT = 161, // 0x00A1
    /// <summary>Left CONTROL key</summary>
    LCONTROL = 162, // 0x00A2
    /// <summary>Right CONTROL key</summary>
    RCONTROL = 163, // 0x00A3
    /// <summary>Left MENU key</summary>
    LMENU = 164, // 0x00A4
    /// <summary>Right MENU key</summary>
    RMENU = 165, // 0x00A5
    /// <summary>Windows 2000/XP: Browser Back key</summary>
    BROWSER_BACK = 166, // 0x00A6
    /// <summary>Windows 2000/XP: Browser Forward key</summary>
    BROWSER_FORWARD = 167, // 0x00A7
    /// <summary>Windows 2000/XP: Browser Refresh key</summary>
    BROWSER_REFRESH = 168, // 0x00A8
    /// <summary>Windows 2000/XP: Browser Stop key</summary>
    BROWSER_STOP = 169, // 0x00A9
    /// <summary>Windows 2000/XP: Browser Search key</summary>
    BROWSER_SEARCH = 170, // 0x00AA
    /// <summary>Windows 2000/XP: Browser Favorites key</summary>
    BROWSER_FAVORITES = 171, // 0x00AB
    /// <summary>Windows 2000/XP: Browser Start and Home key</summary>
    BROWSER_HOME = 172, // 0x00AC
    /// <summary>Windows 2000/XP: Volume Mute key</summary>
    VOLUME_MUTE = 173, // 0x00AD
    /// <summary>Windows 2000/XP: Volume Down key</summary>
    VOLUME_DOWN = 174, // 0x00AE
    /// <summary>Windows 2000/XP: Volume Up key</summary>
    VOLUME_UP = 175, // 0x00AF
    /// <summary>Windows 2000/XP: Next Track key</summary>
    MEDIA_NEXT_TRACK = 176, // 0x00B0
    /// <summary>Windows 2000/XP: Previous Track key</summary>
    MEDIA_PREV_TRACK = 177, // 0x00B1
    /// <summary>Windows 2000/XP: Stop Media key</summary>
    MEDIA_STOP = 178, // 0x00B2
    /// <summary>Windows 2000/XP: Play/Pause Media key</summary>
    MEDIA_PLAY_PAUSE = 179, // 0x00B3
    /// <summary>Windows 2000/XP: Start Mail key</summary>
    LAUNCH_MAIL = 180, // 0x00B4
    /// <summary>Windows 2000/XP: Select Media key</summary>
    LAUNCH_MEDIA_SELECT = 181, // 0x00B5
    /// <summary>Windows 2000/XP: Start Application 1 key</summary>
    LAUNCH_APP1 = 182, // 0x00B6
    /// <summary>Windows 2000/XP: Start Application 2 key</summary>
    LAUNCH_APP2 = 183, // 0x00B7
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_1 = 186, // 0x00BA
    /// <summary>Windows 2000/XP: For any country/region, the '+' key</summary>
    OEM_PLUS = 187, // 0x00BB
    /// <summary>Windows 2000/XP: For any country/region, the ',' key</summary>
    OEM_COMMA = 188, // 0x00BC
    /// <summary>Windows 2000/XP: For any country/region, the '-' key</summary>
    OEM_MINUS = 189, // 0x00BD
    /// <summary>Windows 2000/XP: For any country/region, the '.' key</summary>
    OEM_PERIOD = 190, // 0x00BE
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_2 = 191, // 0x00BF
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_3 = 192, // 0x00C0
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_4 = 219, // 0x00DB
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_5 = 220, // 0x00DC
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_6 = 221, // 0x00DD
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_7 = 222, // 0x00DE
    /// <summary>
    /// Used for miscellaneous characters; it can vary by keyboard.
    /// </summary>
    OEM_8 = 223, // 0x00DF
    /// <summary>
    /// Windows 2000/XP: Either the angle bracket key or the backslash key on the RT 102-key keyboard
    /// </summary>
    OEM_102 = 226, // 0x00E2
    /// <summary>
    /// Windows 95/98/Me, Windows NT 4.0, Windows 2000/XP: IME PROCESS key
    /// </summary>
    PROCESSKEY = 229, // 0x00E5
    /// <summary>
    /// Windows 2000/XP: Used to pass Unicode characters as if they were keystrokes.
    /// The VK_PACKET key is the low word of a 32-bit Virtual Key value used for non-keyboard input methods. For more
    /// information,
    /// see Remark in KEYBDINPUT, SendInput, WM_KEYDOWN, and WM_KEYUP
    /// </summary>
    PACKET = 231, // 0x00E7
    /// <summary>Attn key</summary>
    ATTN = 246, // 0x00F6
    /// <summary>CrSel key</summary>
    CRSEL = 247, // 0x00F7
    /// <summary>ExSel key</summary>
    EXSEL = 248, // 0x00F8
    /// <summary>Erase EOF key</summary>
    EREOF = 249, // 0x00F9
    /// <summary>Play key</summary>
    PLAY = 250, // 0x00FA
    /// <summary>Zoom key</summary>
    ZOOM = 251, // 0x00FB
    /// <summary>Reserved</summary>
    NONAME = 252, // 0x00FC
    /// <summary>PA1 key</summary>
    PA1 = 253, // 0x00FD
    /// <summary>Clear key</summary>
    OEM_CLEAR = 254, // 0x00FE
}
#pragma warning restore CA1720 // Identifiers should not contain type names
#pragma warning disable CA1069 // Enums should not have duplicate values
