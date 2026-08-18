using System;
using SpookyGame.Core.Item_System;
using UnityEngine;

namespace SpookyGame.Utilities
{
    /// <summary>
    /// Manages the Runtime state of a game session. The entry point and over arching tie in to all systems. Allows us to orchestrate a proper Pause system.
    /// </summary>
    public class GameManager : MonoBehaviour
    {
        public static GameManager Instance  { get; private set; }
        

        // =====================================================================
        //  Unity Lifecycle Methods
        // =====================================================================
 
        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
            
            DontDestroyOnLoad(gameObject);
            GameSettings.Load();
        }

        // =====================================================================
        //  Members
        // =====================================================================

        [Header("Lookup Tables")]
        public ItemDatabase ItemDatabase;


        // =====================================================================
        //  Pause System
        // =====================================================================
        
        public bool IsPaused { get; private set; }
        public event Action<bool> PauseChanged = delegate { };  
        
        public void Pause()
        {
            if (IsPaused) return;
            IsPaused = true;
            Time.timeScale = 0f;
            PauseChanged(true);
        }
 
        public void Resume()
        {
            if (!IsPaused) return;
            IsPaused = false;
            Time.timeScale = 1f;
            GameSettings.Save(); // flush anything edited while the menu was open
            PauseChanged(false);
        }
 
        public void TogglePause()
        {
            if (IsPaused) Resume();
            else Pause();
        }

    }
}
