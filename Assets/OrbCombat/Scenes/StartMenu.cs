using UnityEngine;
using UnityEngine.SceneManagement;

namespace OrbCombat
{
    public sealed class StartMenu : MonoBehaviour
    {
        [SerializeField] private Texture2D background;
        [SerializeField] private Texture2D titleImage;
        private const string VolumeKey = "OrbCombat.MasterVolume";
        private bool settingsOpen;

        private void Awake()
        {
            AudioListener.volume = PlayerPrefs.GetFloat(VolumeKey, 1f);
        }

        private void OnGUI()
        {
            var screen = new Rect(0, 0, Screen.width, Screen.height);
            GUI.color = new Color(0.065f, 0.095f, 0.12f);
            GUI.DrawTexture(screen, Texture2D.whiteTexture);
            GUI.color = Color.white;
            if (background) GUI.DrawTexture(screen, background, ScaleMode.ScaleAndCrop);

            var previousMatrix = GUI.matrix;
            float scale = Mathf.Min(Screen.width / 640f, Screen.height / 480f);
            GUI.matrix = Matrix4x4.TRS(new Vector3(Screen.width / 2f, Screen.height / 2f),
                Quaternion.identity, Vector3.one * scale);
            var heading = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 44, fontStyle = FontStyle.Bold };
            var button = new GUIStyle(GUI.skin.button) { fontSize = 21 };
            var titleRect = new Rect(-220, -155, 440, 78);
            if (settingsOpen)
            {
                GUI.Label(titleRect, "Settings", heading);
                var label = new GUIStyle(GUI.skin.label) { fontSize = 19 };
                GUI.Label(new Rect(-125, -40, 250, 35), $"Master Volume   {AudioListener.volume:P0}", label);
                float volume = GUI.HorizontalSlider(new Rect(-125, 4, 250, 28), AudioListener.volume, 0, 1);
                if (!Mathf.Approximately(volume, AudioListener.volume))
                {
                    AudioListener.volume = volume;
                    PlayerPrefs.SetFloat(VolumeKey, volume);
                }
                if (GUI.Button(new Rect(-125, 70, 250, 44), "Back", button))
                {
                    PlayerPrefs.Save();
                    settingsOpen = false;
                }
            }
            else
            {
                if (titleImage) GUI.DrawTexture(titleRect, titleImage, ScaleMode.ScaleToFit);
                else GUI.Label(titleRect, "Orb Combat", heading);
                if (GUI.Button(new Rect(-125, -45, 250, 44), "Start Game", button))
                    SceneManager.LoadScene("Level01_HROffice");
                if (GUI.Button(new Rect(-125, 12, 250, 44), "Settings", button))
                    settingsOpen = true;
                if (GUI.Button(new Rect(-125, 69, 250, 44), "Exit Game", button))
                {
#if UNITY_EDITOR
                    UnityEditor.EditorApplication.isPlaying = false;
#else
                    Application.Quit();
#endif
                }
            }
            GUI.matrix = previousMatrix;
        }
    }
}
