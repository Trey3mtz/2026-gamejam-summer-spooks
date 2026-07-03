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

        private void Awake()
        {
            if (Instance == null)
                Instance = this;
            else
                Destroy(this);
            
            DontDestroyOnLoad(gameObject);
        }

        [Header("Lookup Tables")]
        public ItemDatabase ItemDatabase;
    }
}