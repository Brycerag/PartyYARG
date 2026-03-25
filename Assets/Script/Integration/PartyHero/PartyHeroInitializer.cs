using UnityEngine;
using YARG.Core.Logging;

namespace YARG.Integration.PartyHero
{
    /// <summary>
    /// Bootstraps the <see cref="PartyHeroController"/> singleton into the scene.
    ///
    /// Attach this component to any persistent GameObject in PersistentScene.unity.
    /// The controller creates and manages itself via <see cref="MonoSingleton{T}"/>;
    /// this initializer simply ensures the GameObject exists and logs startup.
    /// </summary>
    public class PartyHeroInitializer : MonoBehaviour
    {
        private void Start()
        {
            if (PartyHeroController.Instance != null)
            {
                YargLogger.LogInfo("[PartyHero] Integration active.");
            }
            else
            {
                YargLogger.LogWarning("[PartyHero] PartyHeroController singleton not found. " +
                    "Ensure a PartyHeroController component exists on a persistent GameObject.");
            }
        }
    }
}
