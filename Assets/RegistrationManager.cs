using System;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

/// <summary>
/// Holds the last registration the player submitted for this app run. Used when exporting the
/// session CSV so name and gender are not lost if <see cref="PlayerPrefs"/> lag or mis-sync.
/// </summary>
public static class PathfinderRegistrationSnapshot
{
    public static bool HasData;
    /// <summary>Matches <see cref="PlayerPrefs"/> SessionId written in the same registration submit.</summary>
    public static string CapturedSessionId;
    public static string PlayerName;
    public static int PlayerAge;
    public static string PlayerGender;
    public static int IsRegistered;
    public static int GameDurationSeconds;

    public static void Clear()
    {
        HasData = false;
        CapturedSessionId = null;
        PlayerName = null;
        PlayerAge = 0;
        PlayerGender = null;
        IsRegistered = 0;
        GameDurationSeconds = 60;
    }

    /// <summary>Call only after <c>PlayerPrefs.Save()</c> for this submit (SessionId must already be set).</summary>
    public static void Capture(string playerName, int playerAge, string playerGender, int isRegistered, int gameDurationSeconds)
    {
        HasData = true;
        CapturedSessionId = PlayerPrefs.GetString("SessionId", "");
        PlayerName = playerName ?? string.Empty;
        PlayerAge = playerAge;
        PlayerGender = playerGender ?? string.Empty;
        IsRegistered = isRegistered;
        GameDurationSeconds = gameDurationSeconds;
    }
}

public class RegistrationManager : MonoBehaviour
{
    const int MinAge = 1;
    const int MaxAge = 120;
    const int MaxNameLength = 32;

    [Header("Input Fields")]
    public InputField nameInput;
    public InputField ageInput;

    [Header("UI")]
    public Text errorMessageText;
    public Button sessionTime1mButton;
    public Button sessionTime2mButton;
    public Button sessionTime5mButton;
    public Button sessionTime10mButton;

    [Header("Buttons")]
    public Button maleButton;
    public Button femaleButton;
    public Button startButton;
    public Button skipButton;

    private string selectedGender = "";
    private readonly Color selectedColor = new Color(0.5f, 1f, 0.5f);
    private readonly Color normalColor = Color.white;
    private int selectedSessionSeconds = 60;

    void Start()
    {
        PathfinderRegistrationSnapshot.Clear();
        selectedSessionSeconds = 60;
        ClearError();
        WireSessionTimeButtons();
        RefreshSessionTimeButtonColors();

        maleButton.onClick.AddListener(() => SelectGender("Male"));
        femaleButton.onClick.AddListener(() => SelectGender("Female"));
        startButton.onClick.AddListener(OnStartClicked);
        skipButton.onClick.AddListener(OnSkipClicked);
    }

    void WireSessionTimeButtons()
    {
        if (sessionTime1mButton != null)
            sessionTime1mButton.onClick.AddListener(() => SelectSessionDuration(60));
        if (sessionTime2mButton != null)
            sessionTime2mButton.onClick.AddListener(() => SelectSessionDuration(120));
        if (sessionTime5mButton != null)
            sessionTime5mButton.onClick.AddListener(() => SelectSessionDuration(300));
        if (sessionTime10mButton != null)
            sessionTime10mButton.onClick.AddListener(() => SelectSessionDuration(600));
    }

    void SelectSessionDuration(int seconds)
    {
        selectedSessionSeconds = seconds;
        RefreshSessionTimeButtonColors();
        ClearError();
    }

    void RefreshSessionTimeButtonColors()
    {
        SetTimeButtonVisual(sessionTime1mButton, selectedSessionSeconds == 60);
        SetTimeButtonVisual(sessionTime2mButton, selectedSessionSeconds == 120);
        SetTimeButtonVisual(sessionTime5mButton, selectedSessionSeconds == 300);
        SetTimeButtonVisual(sessionTime10mButton, selectedSessionSeconds == 600);
    }

    static void SetTimeButtonVisual(Button button, bool selected)
    {
        if (button == null)
            return;
        Image image = button.GetComponent<Image>();
        if (image != null)
            image.color = selected ? new Color(0.5f, 1f, 0.5f) : Color.white;
    }

    void SelectGender(string gender)
    {
        selectedGender = gender;
        PlayerPrefs.SetString("PlayerGender", selectedGender);
        PlayerPrefs.Save();

        maleButton.GetComponent<Image>().color = (gender == "Male") ? selectedColor : normalColor;
        femaleButton.GetComponent<Image>().color = (gender == "Female") ? selectedColor : normalColor;
        ClearError();
    }

    void ShowError(string message)
    {
        if (errorMessageText != null)
            errorMessageText.text = message;
        Debug.Log(message);
    }

    void ClearError()
    {
        if (errorMessageText != null)
            errorMessageText.text = string.Empty;
    }

    /// <summary>
    /// Reads trimmed text from a legacy InputField. If the user clicks Start while the field
    /// still has focus, <see cref="InputField.text"/> can briefly lag; defocus first and fall
    /// back to <see cref="InputField.textComponent"/> when needed.
    /// </summary>
    static string ReadInputFieldTextTrimmed(InputField field)
    {
        if (field == null)
            return string.Empty;

        // Read before deactivation: on some Unity versions/platforms, deactivating first can
        // clear or lag <see cref="InputField.text"/> while the label still shows the typed value.
        string value = field.text != null ? field.text.Trim() : string.Empty;

        if (string.IsNullOrEmpty(value) && field.textComponent != null && field.textComponent.text != null)
        {
            string fromLabel = field.textComponent.text.Trim();
            if (!string.IsNullOrEmpty(fromLabel))
                value = fromLabel;
        }

        if (field.isFocused)
            field.DeactivateInputField();

        return value;
    }

    static void WriteSessionMetadata()
    {
        PlayerPrefs.SetString("SessionId", Guid.NewGuid().ToString());
        PlayerPrefs.SetString("SessionStartUtc", DateTime.UtcNow.ToString("o"));
    }

    public void OnStartClicked()
    {
        ClearError();

        string playerName = ReadInputFieldTextTrimmed(nameInput);
        string ageText = ReadInputFieldTextTrimmed(ageInput);

        if (string.IsNullOrEmpty(playerName))
        {
            ShowError("Please enter your name.");
            return;
        }

        if (playerName.Length > MaxNameLength)
        {
            ShowError("Name must be at most " + MaxNameLength + " characters.");
            return;
        }

        if (string.IsNullOrEmpty(ageText))
        {
            ShowError("Please enter your age.");
            return;
        }

        if (string.IsNullOrEmpty(selectedGender))
        {
            ShowError("Please select Male or Female.");
            return;
        }

        if (!int.TryParse(ageText, out int age))
        {
            ShowError("Please enter a valid whole number for age.");
            return;
        }

        if (age < MinAge || age > MaxAge)
        {
            ShowError("Age must be between " + MinAge + " and " + MaxAge + ".");
            return;
        }

        WriteSessionMetadata();
        PlayerPrefs.SetString("PlayerName", playerName);
        PlayerPrefs.SetInt("PlayerAge", age);
        PlayerPrefs.SetString("PlayerGender", selectedGender);
        PlayerPrefs.SetInt("IsRegistered", 1);
        PlayerPrefs.SetInt("GameDurationSeconds", selectedSessionSeconds);
        PlayerPrefs.Save();

        PathfinderRegistrationSnapshot.Capture(playerName, age, selectedGender, 1, selectedSessionSeconds);

        SceneManager.LoadScene("SnakeScene");
    }

    public void OnSkipClicked()
    {
        ClearError();
        WriteSessionMetadata();
        PlayerPrefs.SetString("PlayerName", "Guest");
        PlayerPrefs.SetInt("PlayerAge", 0);
        PlayerPrefs.SetString("PlayerGender", "");
        PlayerPrefs.SetInt("IsRegistered", 0);
        PlayerPrefs.SetInt("GameDurationSeconds", 60);
        PlayerPrefs.Save();

        PathfinderRegistrationSnapshot.Capture("Guest", 0, "", 0, 60);

        SceneManager.LoadScene("SnakeScene");
    }
}
