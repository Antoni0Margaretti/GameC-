using UnityEngine;

public static class KeyBindings
{
    public static KeyCode Dash = KeyCode.LeftShift;
    public static KeyCode Parry = KeyCode.Mouse1;
    public static KeyCode Attack = KeyCode.Mouse0;
    public static KeyCode Pause = KeyCode.Escape;
    public static KeyCode Slide = KeyCode.LeftControl;
    public static KeyCode Crouch = KeyCode.S;

    static KeyBindings()
    {
        Dash = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Dash", Dash.ToString()));
        Parry = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Parry", Parry.ToString()));
        Attack = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Attack", Attack.ToString()));
        Pause = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Pause", Pause.ToString()));
        Slide = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Slide", Slide.ToString()));
        Crouch = (KeyCode)System.Enum.Parse(typeof(KeyCode), PlayerPrefs.GetString("Key_Crouch", Crouch.ToString()));
    }

    public static void Save()
    {
        PlayerPrefs.SetString("Key_Dash", Dash.ToString());
        PlayerPrefs.SetString("Key_Parry", Parry.ToString());
        PlayerPrefs.SetString("Key_Attack", Attack.ToString());
        PlayerPrefs.SetString("Key_Pause", Pause.ToString());
        PlayerPrefs.SetString("Key_Slide", Slide.ToString());
        PlayerPrefs.SetString("Key_Crouch", Crouch.ToString());
        PlayerPrefs.Save();
    }
}