using UnityEngine;
using YARG;
using YARG.Core.Input;
using YARG.Menu.Navigation;

namespace YARG.Menu.ShowEnd
{
    /// <summary>
    /// Shown after the final song of the show (or when a ShowEnd entry is reached).
    /// Returns to the main menu on Green confirmation.
    /// </summary>
    public class ShowEndMenu : MonoBehaviour
    {
        private void OnEnable()
        {
            Navigator.Instance.PushScheme(new NavigationScheme(new()
            {
                new NavigationScheme.Entry(MenuAction.Green, "Menu.ShowEnd.ReturnToMenu", GoToMenu),
            }, false));
        }

        private void OnDisable()
        {
            Navigator.Instance?.PopScheme();
        }

        public void GoToMenu()
        {
            GlobalVariables.Instance.LoadScene(SceneIndex.Menu);
        }
    }
}
